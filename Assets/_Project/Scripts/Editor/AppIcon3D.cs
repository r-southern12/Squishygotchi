using System.IO;
using Squishy.Runtime.Models;
using Squishy.Runtime.Three;
using Squishy.Runtime.World;
using Squishy.Simulation.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Squishy.EditorTools
{
    /// <summary>
    /// Icon candidates made from the real in-game squishy: the glossy 3D model rendered close up with the
    /// game's lighting, composited over a bold sunburst ground. Menu: Squishy > Icon Variants (3D).
    /// </summary>
    public static class AppIcon3D
    {
        private const string Dir = "Assets/_Project/Art/Icon";
        private const int S = 512;

        [MenuItem("Squishy/Icon Variants (3D)")]
        public static void Variants()
        {
            Directory.CreateDirectory(Dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var c = JsonUtility.FromJson<GameContent>(File.ReadAllText("Assets/_Project/Resources/Content/game_content.json"));
            c.Init();
            var rules = new GameRules(c, GameRules.NewState(c, 1));

            var world = new GameObject("World").transform;
            world.localScale = new Vector3(1, 1, -1);
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = ThreeMat.Hex("#FFE6C8");
            key.intensity = 1.9f;
            SceneLighting.Key(key);
            SceneLighting.Globals();
            SceneLighting.ClearLamps();
            Post.ThumbMode(true);

            var pet = new SquishyModel(world, .5f);
            pet.SetFinish(rules.Fav);
            pet.SetStage(GameRules.Life.Adult);

            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.fieldOfView = 24;
            cam.aspect = 1;
            cam.nearClipPlane = .02f;
            cam.farClipPlane = 50;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var rt = new RenderTexture(S, S, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { antiAliasing = 8 };
            cam.targetTexture = rt;

            float[] yaw = { 0, .35f, -.3f };
            float[] squash = { 0, .12f, -.06f };
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false, false);
            for (int v = 0; v < 3; v++)
            {
                var sheet = new Canvas2D(S, S);
                float ox = 0, oy = 0;
                Ground(sheet, ox, oy, v);
                pet.Yaw.RotY(yaw[v]);
                pet.EyeOpen = 1;
                pet.Update(0, squash[v], false, 0);
                var bb = Node.LocalBounds(pet.Pivot, world);
                Vector3 ctr = bb.center + new Vector3(0, bb.size.y * .02f, 0);
                float r = Mathf.Max(bb.size.x, bb.size.y) * .5f;
                var dir = new Vector3(0, .32f, 1).normalized;
                var pos = ctr + dir * (r / Mathf.Tan(12 * Mathf.Deg2Rad) * 1.08f);
                cam.transform.position = Space3.U(pos);
                cam.transform.rotation = Quaternion.LookRotation(Space3.U(ctr) - Space3.U(pos), Vector3.up);
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, S, S), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                Composite(sheet, tex, ox, oy);
                var outTex = sheet.ToTexture(true, false, false, "icon", true);
                File.WriteAllBytes(Path.Combine(Dir, "icon3d_" + v + ".png"), outTex.EncodeToPNG());
            }
            Post.ThumbMode(false);
        }

        private static void Ground(Canvas2D g, float ox, float oy, int v)
        {
            string[] grounds = { "#1F8A8A", "#E2553A", "#F5B82E" };
            string[] glows = { "#7FD6C8", "#FFA07A", "#FFE9A0" };
            Color ground = Canvas2D.Css(grounds[v]), glow = Canvas2D.Css(glows[v]);
            float rad = .22f * S;
            // Rounded tile: sunburst rays and a soft centre glow, masked to the tile.
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float cx = Mathf.Clamp(x, rad, S - rad), cy = Mathf.Clamp(y, rad, S - rad);
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float cover = Mathf.Clamp01(rad - d + .5f);
                if (cover <= 0) continue;
                float dx = x / (float)S - .5f, dy = y / (float)S - .5f, dist = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float ray = Mathf.Sin(ang * 12) > .35f ? .12f : 0;
                var col = Color.Lerp(ground, glow, Mathf.Clamp01(1 - dist / .55f) * .55f + ray * (1 - dist));
                g.Set((int)(ox + x), (int)(oy + y), col, cover);
            }
        }

        private static void Composite(Canvas2D g, Texture2D tex, float ox, float oy)
        {
            // Soft contact shadow, then the render (the readback is bottom-up; the canvas is top-down).
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                Color c = tex.GetPixel(x, S - 1 - y);
                if (c.a <= 0.002f) continue;
                c.a = Mathf.Clamp01(c.a);
                g.Set((int)(ox + x), (int)(oy + y), new Color(c.r, c.g, c.b, 1), c.a);
            }
        }
    }
}
