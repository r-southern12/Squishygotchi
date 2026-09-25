using Squishy.Simulation.Content;
using Squishy.Simulation.Room;
using UnityEngine;

namespace Squishy.Runtime.Room
{
    /// <summary>A piece standing in the room: its save entry, type, activity and where the squishy stands to use it.</summary>
    public sealed class RoomItem : MonoBehaviour
    {
        public PlacedPiece Piece { get; private set; }
        public ItemTypeDef Type { get; private set; }
        public ActivityDef Activity { get; private set; }
        public PlaceholderShape Shape { get; private set; }

        /// <summary>Lamp state. Runtime only for now; saved with the room in Milestone 4.</summary>
        public bool LampOn { get; private set; } = true;
        private Light _lampLight;
        private Renderer _lampShade;
        private Color _shadeOn, _shadeOff;

        public float Radius { get { return Shape.radius; } }
        /// <summary>True when the squishy climbs onto or into the item (bed, cushion, bath...).</summary>
        public bool Perch { get { return Shape.perchHeight > 0f; } }
        public float PerchHeight { get { return Shape.perchHeight; } }
        /// <summary>Walk-through items (rugs) don't block the squishy.</summary>
        public bool Blocks { get { return Shape.blocks; } }
        public Vector3 Front { get { return transform.forward; } }

        public void Init(PlacedPiece piece, ItemTypeDef type, ActivityDef activity, PlaceholderShape shape)
        {
            Piece = piece;
            Type = type;
            Activity = activity;
            Shape = shape;

            // One box collider over everything, for tapping.
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var r in GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = transform.InverseTransformPoint(bounds.center);
            col.size = bounds.size + Vector3.one * 0.05f;

            if (type.id == "lamp")
            {
                _lampShade = shape.highlight;
                if (_lampShade != null)
                {
                    _shadeOn = _lampShade.sharedMaterial.GetColor("_BaseColor");
                    _shadeOff = Color.Lerp(_shadeOn, Color.gray, 0.5f);
                }
                var lightGo = new GameObject("Glow");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 0.9f;
                _lampLight = lightGo.AddComponent<Light>();
                _lampLight.type = LightType.Point;
                _lampLight.range = 2.2f;
                _lampLight.intensity = 1.2f;
                _lampLight.color = new Color(1f, 0.85f, 0.6f);
                _lampLight.shadows = LightShadows.None;
            }
        }

        public void ToggleLamp()
        {
            LampOn = !LampOn;
            if (_lampLight != null) _lampLight.enabled = LampOn;
            if (_lampShade != null) _lampShade.material.SetColor("_BaseColor", LampOn ? _shadeOn : _shadeOff);
        }

        private Vector3 _rollFrom, _rollTo;
        private float _rollT = 1f;

        /// <summary>Kicked ball: bounce-rolls to a new spot and remembers it in the save.</summary>
        public void Roll(Vector3 target, float floorY)
        {
            _rollFrom = transform.position;
            _rollTo = new Vector3(target.x, floorY, target.z);
            _rollT = 0f;
            Piece.x = _rollTo.x;
            Piece.z = _rollTo.z;
        }

        private void Update()
        {
            if (_rollT >= 1f) return;
            _rollT = Mathf.Min(1f, _rollT + Time.deltaTime / 0.9f);
            float e = 1f - (1f - _rollT) * (1f - _rollT);
            Vector3 p = Vector3.Lerp(_rollFrom, _rollTo, e);
            p.y += 0.25f * Mathf.Abs(Mathf.Sin(_rollT * Mathf.PI * 2f)) * (1f - _rollT);
            transform.position = p;
            transform.Rotate(Vector3.right, Time.deltaTime * 720f * (1f - _rollT), Space.Self);
        }

        /// <summary>Where the squishy stands (or climbs on) to use this, and the point it should face.</summary>
        public void UseSpot(Vector3 from, float squishyRadius, out Vector3 stand, out Vector3 approach, out float standHeight, out Vector3 face)
        {
            Vector3 centre = transform.position;
            if (Perch)
            {
                Vector3 side = from - centre;
                side.y = 0f;
                if (side.sqrMagnitude < 1e-4f) side = Front;
                approach = centre + side.normalized * (Radius + squishyRadius + 0.1f);
                stand = centre;
                standHeight = PerchHeight;
                face = centre + Front;
                return;
            }
            Vector3 dir = Shape.faceCentre ? -centre : Front;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            stand = centre + dir.normalized * (Radius + squishyRadius + 0.05f);
            approach = stand;
            standHeight = 0f;
            face = centre;
        }
    }
}
