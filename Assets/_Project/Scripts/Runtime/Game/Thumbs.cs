using System.Collections.Generic;
using Squishy.Runtime.Models;
using Squishy.Runtime.Three;
using Squishy.Runtime.World;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// renderThumb: 144px catalogue thumbnails rendered with the same lights and tone curve as the game,
    /// on a transparent background, then cached.
    /// </summary>
    public static class Thumbs
    {
        private const int TS = 384; // the prototype used 144; sharper for phone screens
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static SteamerGame _game;
        private static GameContent _c;
        private static GameRules _rules;
        private static Transform _root;
        private static Camera _cam;
        private static RenderTexture _rt;
        private static SquishyModel _pet;

        public static void Init(SteamerGame game, GameContent c, GameRules rules, Transform world, Light key)
        {
            _game = game;
            _c = c;
            _rules = rules;
            _root = Node.Group(world, "Thumbs", 0, -200, 0);
            _cam = new GameObject("Thumb Camera").AddComponent<Camera>();
            _cam.enabled = false;
            _cam.fieldOfView = 26;
            _cam.aspect = 1;
            _cam.nearClipPlane = .02f;
            _cam.farClipPlane = 50;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0, 0, 0, 0);
            _cam.cullingMask = 1 << SteamerGame.ThumbLayer;
            _cam.allowMSAA = true;
            var data = _cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            _rt = new RenderTexture(TS, TS, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            _rt.antiAliasing = 4;
            _cam.targetTexture = _rt;
            _pet = new SquishyModel(_root, .5f);
            _pet.Pivot.gameObject.SetActive(false);
        }

        /// <summary>Frames an object like the prototype's thumbnails and renders it with the game's look.</summary>
        private static Texture2D RenderTex(Transform obj)
        {
            var world = _root.parent;
            var bb = Node.LocalBounds(obj, world);
            Vector3 ctr = bb.center, sz = bb.size;
            float r = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)) * .62f + .02f;
            var dir = new Vector3(.55f, .62f, 1).normalized;
            var pos = ctr + dir * (r / Mathf.Tan(13 * Mathf.Deg2Rad));
            _cam.transform.position = Space3.U(pos);
            _cam.transform.rotation = Quaternion.LookRotation(Space3.U(ctr) - Space3.U(pos), Vector3.up);
            SceneLighting.ClearLamps();
            Post.ThumbMode(true);
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = _rt };
            if (RenderPipeline.SupportsRenderRequest(_cam, req)) RenderPipeline.SubmitRenderRequest(_cam, req);
            else _cam.Render();
            Post.ThumbMode(false);
            _game.RestoreLamps();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(TS, TS, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, TS, TS), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        /// <summary>A picture of a squishy in a mood (grey, squash, eyes shut) as PNG bytes, for widgets and notifications.</summary>
        public static byte[] SquishyPng(FinishData f, Squishy.Simulation.Game.GameRules.Life stage, float grey, float squash, bool closed)
        {
            if (_cam == null) return null;
            _pet.Pivot.gameObject.SetActive(true);
            _pet.SetFinish(f);
            _pet.SetStage(stage);
            _pet.Grey = grey;
            _pet.Yaw.RotY(.35f);
            _pet.X = 0;
            _pet.V = 0;
            _pet.EyeOpen = closed ? .08f : 1;
            _pet.Update(0, squash, closed, grey > .3f ? 1 : 0);
            var tex = RenderTex(_pet.Pivot);
            _pet.Pivot.gameObject.SetActive(false);
            var png = tex.EncodeToPNG();
            Object.Destroy(tex);
            return png;
        }

        public static Texture2D Cached(string key) { return Cache.TryGetValue(key, out var t) ? t : null; }
        public static void Forget(string key) { Cache.Remove(key); }

        public static Texture2D Get(string key)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            if (_cam == null) return null;
            var parts = key.Split(':');
            string kind = parts[0];
            Transform obj;
            if (kind == "sq")
            {
                _pet.Pivot.gameObject.SetActive(true);
                _pet.SetFinish(_c.finishes[int.Parse(parts[1])]);
                _pet.Yaw.RotY(.35f);
                _pet.X = 0;
                _pet.Update(0, 0, false);
                obj = _pet.Pivot;
            }
            else if (kind == "tool") { obj = KitchenModels.Tool(_c, _rules.S, int.Parse(parts[1]), null, _root); obj.RotY(.6f); }
            else if (kind == "food") { obj = KitchenModels.Food(_c, int.Parse(parts[1]), _root); obj.RotY(.5f); }
            else if (kind == "dish") obj = KitchenModels.Dish(_c, int.Parse(parts[1]), _root);
            else if (kind == "snack") obj = KitchenModels.Snack(_c, int.Parse(parts[1]), _root);
            else if (kind == "steamerbox")
            {
                obj = Node.Group(_root, "steamerbox");
                new SteamerModel(obj, 24);
                SteamerModel.Lid(obj).localPosition = new Vector3(0, SteamerModel.H + .02f, 0);
            }
            else if (kind == "tskin") { obj = KitchenModels.Tool(_c, _rules.S, int.Parse(parts[1]), int.Parse(parts[2]), _root); obj.RotY(.6f); }
            else { obj = ItemModels.Build(_c, parts[0], parts.Length > 1 ? parts[1] : "", _root, new ItemParts()); obj.RotY(-.5f); }
            Node.SetLayer(obj, SteamerGame.ThumbLayer);
            var tex = RenderTex(obj);
            tex.name = "thumb " + key;
            if (kind == "sq") _pet.Pivot.gameObject.SetActive(false);
            else { obj.gameObject.SetActive(false); Node.Destroy(obj); } // hide now: Destroy waits for frame end and the next thumbnail would see it
            return Cache[key] = tex;
        }
    }
}
