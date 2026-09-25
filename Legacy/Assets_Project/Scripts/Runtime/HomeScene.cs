using System.Collections.Generic;
using Squishy.Data;
using Squishy.Runtime.CameraRig;
using Squishy.Runtime.Rendering;
using Squishy.Runtime.Room;
using Squishy.Runtime.SquishyPet;
using Squishy.Runtime.UI;
using Squishy.Simulation.Care;
using Squishy.Simulation.Content;
using Squishy.Simulation.Room;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Squishy.Runtime
{
    /// <summary>
    /// The home view: builds the room, its furniture and the favourite from the save, runs needs in
    /// real time, turns taps into requests, and handles death and the next generation.
    /// </summary>
    public sealed class HomeScene : MonoBehaviour
    {
        [SerializeField] private SteamerRoom room;
        [SerializeField] private SquishyBody squishy;
        [SerializeField] private FollowCamera followCamera;
        [SerializeField] private TiltShiftController tiltShift;
        [Tooltip("Game speed when testing with F (editor and development builds only).")]
        [SerializeField] private float fastForward = 30f;
        [SerializeField] private HomeLighting lighting = new HomeLighting();

        private GameBootstrap _game;
        private ContentDatabase _content;
        private SquishyBrain _brain;
        private CareHud _hud;
        private readonly List<RoomItem> _items = new List<RoomItem>();
        private PlaceholderFurniture _furniture;
        private int _comfort;
        private bool _deathShown;

        private void Start()
        {
            _game = GameBootstrap.Instance;
            if (_game == null || _game.Save == null || _game.Content.care == null)
            {
                Debug.LogError("HomeScene needs a loaded save and a Care asset. Run Squishy > Setup > Run Full Setup.", this);
                enabled = false;
                return;
            }
            _content = _game.Content;
            var save = _game.Save;
            lighting.Apply(Camera.main);

            var level = Growth.RoomLevel(_content.progression.roomLevels, save.roomLevel);
            room.Build(level != null ? level.relativeWidth : 0.77f);
            _furniture = new PlaceholderFurniture(room.Palette);
            foreach (var piece in save.pieces) if (!piece.inStorage) Spawn(piece);
            _comfort = _game.Comfort();

            _brain = squishy.GetComponent<SquishyBrain>();
            if (_brain == null) _brain = squishy.gameObject.AddComponent<SquishyBrain>();
            BuildSquishy();

            _hud = gameObject.AddComponent<CareHud>();
            _hud.Build();
            _hud.WholeRoomPressed += followCamera.ToggleWholeRoom;
            followCamera.Tapped += OnTap;
        }

        private void BuildSquishy()
        {
            var save = _game.Save;
            string id = FavouriteId();
            var finish = _content.finishes.Find(f => f.def.id == id);
            int copies = Mathf.Max(1, Growth.CopiesOf(save.squishies, id));
            var size = Growth.SizeFor(_content.progression.sizeTiers, copies);
            float scale = size != null ? size.relativeScale : 1f;

            squishy.Build(scale, finish != null ? finish.baseColor : Color.white, finish != null ? finish.smoothness : 0.5f);
            _brain.Init(room, _items, _content.care, save.care, size != null ? size.size : SquishySize.Mini, scale, SnackCount, UseSnack);
            followCamera.Init(room, squishy.transform, squishy.Radius);
            if (tiltShift != null) tiltShift.SetFocus(squishy.transform, squishy.Radius);
        }

        private void Update()
        {
            if (_brain == null) return;
            var save = _game.Save;
            var def = _content.care.care;

            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame && (Application.isEditor || Debug.isDebugBuild))
                _game.TimeScale = _game.TimeScale > 1f ? 1f : fastForward;

            if (!save.care.dead)
            {
                float slow = CareSim.ComfortSlowdown(_comfort, _content.economy.def);
                CareSim.Tick(save.care, def, Time.deltaTime * _game.TimeScale, slow);
            }
            if (save.care.dead && !_deathShown) OnDeath();

            _hud.Refresh(save.care, def, _comfort, _game.TimeScale, Camera.main,
                squishy.transform.position + Vector3.up * squishy.Radius * 2.6f, _brain.Status, _brain.StatusUrgent);
        }

        private void OnTap(Vector2 screen)
        {
            if (_game.Save.care.dead || Camera.main == null) return;
            var ray = Camera.main.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 100f)) return;
            if (hit.collider.GetComponentInParent<SquishyBody>() != null)
            {
                squishy.Impulse(5f); // squish!
                return;
            }
            var item = hit.collider.GetComponentInParent<RoomItem>();
            if (item != null) _brain.RequestUse(item);
        }

        // ---- Death and the next generation --------------------------------------------

        private void OnDeath()
        {
            _deathShown = true;
            var save = _game.Save;
            string id = FavouriteId();

            // A tombstone keepsake where it fell.
            var p = squishy.transform.position;
            var stone = new PlacedPiece
            {
                instanceId = save.nextPieceId++,
                itemTypeId = "tombstone",
                skinId = PlacedPiece.SkinIdFor("tombstone", id),
                x = p.x,
                z = p.z,
                yawDegrees = squishy.transform.eulerAngles.y,
            };
            save.pieces.Add(stone);
            Spawn(stone);
            squishy.gameObject.SetActive(false);
            _game.WriteSave();

            var choices = new List<KeyValuePair<string, string>>();
            foreach (var owned in save.squishies)
            {
                if (owned.count <= 0) continue;
                var f = _content.finishes.Find(x => x.def.id == owned.id);
                choices.Add(new KeyValuePair<string, string>(owned.id, f != null ? f.def.displayName : owned.id));
            }
            if (choices.Count == 0) choices.Add(new KeyValuePair<string, string>(id, NameOf(id)));
            _hud.ShowDeath(NameOf(id), save.care.causeOfDeath, choices, ChooseNext);
        }

        private void ChooseNext(string finishId)
        {
            var save = _game.Save;
            save.favouriteSquishyId = finishId;
            CareSim.StartNextGeneration(save.care);
            _deathShown = false;
            _hud.HideDeath();
            squishy.gameObject.SetActive(true);
            BuildSquishy();
            _game.WriteSave();
        }

        // ---- Helpers ------------------------------------------------------------------

        private void Spawn(PlacedPiece piece)
        {
            var typeAsset = _content.ItemType(piece.itemTypeId);
            if (typeAsset == null) { Debug.LogWarning("Unknown item type " + piece.itemTypeId); return; }
            var style = _content.Style(piece.StyleId);

            var go = new GameObject(piece.skinId);
            go.transform.SetParent(room.transform, false);
            go.transform.localPosition = new Vector3(piece.x, room.FloorY, piece.z);
            go.transform.localRotation = Quaternion.Euler(0f, piece.yawDegrees, 0f);
            var shape = _furniture.Build(piece.itemTypeId, go.transform, style != null ? style.palette : null);
            var item = go.AddComponent<RoomItem>();
            item.Init(piece, typeAsset.def, _content.care.ActivityForItem(piece.itemTypeId), shape);
            _items.Add(item);
        }

        private string FavouriteId()
        {
            var save = _game.Save;
            return string.IsNullOrEmpty(save.favouriteSquishyId) ? _content.economy.def.startingSquishyId : save.favouriteSquishyId;
        }

        private string NameOf(string finishId)
        {
            var f = _content.finishes.Find(x => x.def.id == finishId);
            return f != null ? f.def.displayName : "Your squishy";
        }

        private int SnackCount()
        {
            int n = 0;
            foreach (var s in _game.Save.snacks) n += s.count;
            return n;
        }

        private void UseSnack()
        {
            foreach (var s in _game.Save.snacks)
                if (s.count > 0) { s.count--; return; }
        }
    }
}
