using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using Squishy.Runtime.World;
using UnityEngine;

namespace Squishy.EditorTools
{
    /// <summary>
    /// Photographs the real game for icon options: plays Main.unity, waits for the room to settle, then renders
    /// the squishy in its steamer room from a few camera angles through the game's own lighting and tilt-shift.
    /// Batch (no -quit): -executeMethod Squishy.EditorTools.IconShots.Run. Writes Art/Icon/shot_*.png, then exits.
    /// </summary>
    [InitializeOnLoad]
    public static class IconShots
    {
        private const string Key = "Squishy.IconShots", Dir = "Library/IconChecks"; // renders for checking, kept out of Assets
        private const int Size = 1024;

        // name, elevation (deg), frame height as a multiple of the squishy's height, field of view
        private static readonly (string name, float elev, float fill, float fov)[] Shots =
        {
            ("portrait", 14, 2.1f, 30),
            ("hero_low", 4, 1.6f, 34),
            ("diorama", 48, 11f, 30),
            ("diorama_close", 30, 5.5f, 30),
        };

        static IconShots()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode) EditorApplication.update += Tick;
            };
        }

        public static void Run()
        {
            EditorUtility.audioMasterMute = true; // test runs play the game: never through the owner's speakers
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
            EditorApplication.EnterPlaymode();
        }

        private static int _start = -1;
        private static int _bathAt = -1, _inBath = -1;
        private static Transform _tub;
        private static float _ground = float.MaxValue, _lastY = float.MaxValue;

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            AudioListener.volume = 0; // silent test runs
            if (_start < 0) _start = Time.frameCount;
            int f = Time.frameCount - _start;
            if (f > 4000) Finish(1);
            if (f < 180) return; // let the room build, the squishy settle and thumbnails finish
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_SOUNDS") == "1")
            {
                if (f < 60) return;
                EditorApplication.update -= Tick;
                var bank = new Squishy.Runtime.Game.SoundBank();
                int ok = 0, bad = 0;
                var sb = new System.Text.StringBuilder();
                foreach (var o in bank.AllOptions())
                {
                    if (o == "none") continue;
                    var a = bank.Get(o, 1.2f, false);
                    if (a == null) { bad++; sb.Append(" MISSING:").Append(o); } else { ok++; sb.Append(" ").Append(o).Append("=").Append(a.length.ToString("F2")); }
                }
                foreach (var e in bank.Table.events) foreach (var o in e.options) if (o != "none" && !bank.AllOptions().Contains(o)) { bad++; sb.Append(" UNKNOWN:").Append(e.id).Append("/").Append(o); }
                Debug.Log("IconShots: sounds ok=" + ok + " bad=" + bad + sb);
                Finish(0);
                return;
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_ADMIN") == "1")
            {
                if (f < 120) return;
                EditorApplication.update -= Tick;
                var g = Squishy.Runtime.Game.SteamerGame.I;
                try { g.OnSettings();
                    UnityEngine.UIElements.VisualElement btn = null;
                    foreach (var doc in Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None))
                        UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.Label>(doc.rootVisualElement).ForEach(l => { if (l.text == "Admin tools (testing)") btn = l.parent; });
                    if (btn == null) Debug.LogError("IconShots: admin button not found");
                    else { var c = btn.worldBound.center; var hit = btn.panel.Pick(c); Debug.Log("IconShots: admin button at " + btn.worldBound + " picks " + (hit == null ? "null" : hit.GetType().Name + " name=" + hit.name + " parentIsBtn=" + (hit == btn || hit.parent == btn)) + " panelBounds=" + btn.panel.visualTree.worldBound); }
                    g.OnAdmin(); Debug.Log("IconShots: admin ok, mode=" + typeof(Squishy.Runtime.Game.SteamerGame).GetField("mode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(g)); }
                catch (System.Exception e) { Debug.LogError("IconShots: admin threw " + e); }
                Finish(0);
                return;
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_COIN") == "1")
            {
                // The tip coin over an energised squishy.
                if (f < 60) return;
                var game = Squishy.Runtime.Game.SteamerGame.I;
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                if (f < 64) { var st = ((Squishy.Simulation.Game.GameRules)typeof(Squishy.Runtime.Game.SteamerGame).GetField("Rules", flags | System.Reflection.BindingFlags.Public).GetValue(game)).S; for (int k = 0; k < 4; k++) st.needs[k] = 1; }
                typeof(Squishy.Runtime.Game.SteamerGame).GetField("energyT", flags).SetValue(game, 60f);
                if (f < 140) return;
                EditorApplication.update -= Tick;
                var cam = Camera.main;
                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
                var flat = cam.transform.position - Bounds(FindPet()).center;
                flat.y = 0;
                Directory.CreateDirectory(Dir);
{ var T1 = typeof(Squishy.Runtime.Game.SteamerGame); Debug.Log("IconShots: mode=" + T1.GetField("mode", flags).GetValue(game) + " visiting=" + T1.GetField("visiting", flags).GetValue(game)); T1.GetMethod("StepTipCoin", flags).Invoke(game, new object[] { .3f }); }
                { var T0 = typeof(Squishy.Runtime.Game.SteamerGame); var tc = (Transform)T0.GetField("tipCoin", flags).GetValue(game); Debug.Log("IconShots: coin " + (tc == null ? "null" : tc.gameObject.activeInHierarchy + " pos " + tc.position + " scale " + tc.localScale + " layer " + tc.gameObject.layer) + " energyT " + T0.GetField("energyT", flags).GetValue(game) + " pet " + FindPet().position); }
                                Shot(cam, rt, tex, Bounds(FindPet()), flat.normalized, 20, 4f, 30, Shader.GetGlobalFloat("_PostWarm"), "coin");
                Finish(0);
                return;
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_FX") == "1")
            {
                // The rarity sparkles: a glitter squishy squished a few times, photographed mid-shimmer.
                if (f < 90) return;
                var game = Squishy.Runtime.Game.SteamerGame.I;
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var T = typeof(Squishy.Runtime.Game.SteamerGame);
                var petM = (Squishy.Runtime.Models.SquishyModel)T.GetField("pet", flags).GetValue(game);
                var content = (Squishy.Simulation.Game.GameContent)T.GetField("C", flags | System.Reflection.BindingFlags.Public).GetValue(game);
                int fi = System.Array.FindIndex(content.finishes, x => x.name == "Rainbow Fizz");
                if (f == 90) petM.SetFinish(content.finishes[fi]);
                var fx = T.GetMethod("SquishFx", flags);
                var pw = T.GetMethod("PetWorld", flags);
                if (f < 150) { if (f % 12 == 0) fx.Invoke(game, new object[] { (Vector3)pw.Invoke(game, null) + Vector3.up * petM.Scale * .6f, 10, .35f }); return; }
                EditorApplication.update -= Tick;
                var cam = Camera.main;
                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
                var flat = cam.transform.position - Bounds(FindPet()).center;
                flat.y = 0;
                Directory.CreateDirectory(Dir);
                Shot(cam, rt, tex, Bounds(FindPet()), flat.normalized, 25, 3.2f, 30, Shader.GetGlobalFloat("_PostWarm"), "fx");
                Finish(0);
                return;
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_VISIT") == "1")
            {
                // Smoke test of a visit: start a test visit, photograph it, go home again, and report.
                var game = Squishy.Runtime.Game.SteamerGame.I;
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var T = typeof(Squishy.Runtime.Game.SteamerGame);
                bool vis = (bool)T.GetField("visiting", flags).GetValue(game);
                if (_bathAt < 0) { T.GetMethod("TestVisit", flags).Invoke(game, null); _bathAt = f; return; }
                if (!vis) { if (f - _bathAt > 600) { Debug.LogError("IconShots: visit never started"); Finish(1); } return; }
                if (_inBath < 0) _inBath = f;
                if (f - _inBath < 120) return;
                EditorApplication.update -= Tick;
                var vpet = FindPet();
                var cam = Camera.main;
                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
                var flat = cam.transform.position - Bounds(vpet).center;
                flat.y = 0;
                Directory.CreateDirectory(Dir);
                Shot(cam, rt, tex, Bounds(vpet), flat.normalized, 30, 6f, 30, Shader.GetGlobalFloat("_PostWarm"), "visit");
                T.GetMethod("EndVisit", flags).Invoke(game, new object[] { true });
                Debug.Log("IconShots: visit ok, back home = " + !(bool)T.GetField("visiting", flags).GetValue(game));
                Finish(0);
                return;
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_BATH") == "1")
            {
                // Send the squishy to its bath and photograph it once it has been in the water a moment.
                var game = Squishy.Runtime.Game.SteamerGame.I;
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var ai = typeof(Squishy.Runtime.Game.SteamerGame).GetField("ai", flags).GetValue(game);
                string mode = (string)ai.GetType().GetField("mode").GetValue(ai);
                if (_bathAt < 0)
                {
                    var items = (System.Collections.IList)typeof(Squishy.Runtime.Game.SteamerGame).GetField("items", flags).GetValue(game);
                    foreach (var it in items)
                        if ((string)it.GetType().GetProperty("arch").GetValue(it) == "tub")
                        {
                            typeof(Squishy.Runtime.Game.SteamerGame).GetMethod("UseItem", flags).Invoke(game, new object[] { it, true, null });
                            _bathAt = f;
                            _tub = ((Transform)it.GetType().GetField("g").GetValue(it));
                        }
                    if (_bathAt < 0) { Debug.LogError("no tub"); Finish(1); }
                    return;
                }
                if (mode != "act") { _inBath = -1; return; }
                if (_inBath < 0) _inBath = f;
                if (f - _inBath < 90) return;
                EditorApplication.update -= Tick;
                var cam = Camera.main;
                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
                var bb = Bounds(_tub);
                var flat = cam.transform.position - bb.center;
                flat.y = 0;
                Directory.CreateDirectory(Dir);
                Shot(cam, rt, tex, bb, flat.normalized, 38, 2.4f, 30, Shader.GetGlobalFloat("_PostWarm"), "bath");
                Finish(0);
                return;
            }
            // Wait for the squishy to be resting on the floor (not mid-hop).
            var pet = FindPet();
            if (pet != null)
            {
                float y = Bounds(pet).min.y;
                _ground = Mathf.Min(_ground, y);
                bool still = Mathf.Abs(y - _lastY) < 1e-4f;
                _lastY = y;
                if (f < 1500 && (f < 400 || !still || y - _ground > .002f)) return;
            }
            EditorApplication.update -= Tick;
            try { Capture(); Finish(0); }
            catch (System.Exception e) { Debug.LogError("IconShots failed: " + e); Finish(1); }
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(code);
        }

        private static void Capture()
        {
            var pet = FindPet();
            if (pet == null) throw new System.Exception("home squishy not found");
            var bb = Bounds(pet);
            var cam = Camera.main;
            // Face the camera the same way the game camera looks, so the near wall is already cut away.
            var flat = cam.transform.position - bb.center;
            flat.y = 0;
            flat.Normalize();
            FaceTowards(pet, flat);
            float warm = Shader.GetGlobalFloat("_PostWarm");
            var pos0 = cam.transform.position;
            var rot0 = cam.transform.rotation;
            float fov0 = cam.fieldOfView;
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
            Directory.CreateDirectory(Dir);
            foreach (var s in Shots)
            {
                cam.fieldOfView = s.fov;
                float h = bb.size.y * s.fill;
                float dist = h * .5f / Mathf.Tan(s.fov * .5f * Mathf.Deg2Rad);
                var dir = Quaternion.AngleAxis(-s.elev, Vector3.Cross(Vector3.up, flat)) * flat;
                var target = bb.center + Vector3.up * bb.size.y * (s.fill > 4 ? 0 : .08f);
                cam.transform.position = target + dir.normalized * dist;
                cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
                var prevT = cam.targetTexture;
                float aspect = cam.aspect;
                cam.targetTexture = rt;
                cam.aspect = 1;
                var vp = cam.WorldToViewportPoint(bb.center);
                Post.Set(vp.y, s.fill > 4 ? .16f : .13f, warm, 0);
                cam.Render();
                cam.targetTexture = prevT;
                cam.aspect = aspect;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(System.IO.Path.Combine(Dir, "shot_" + s.name + ".png"), tex.EncodeToPNG());
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_EXTRA") == "1") Extras(bb, cam, flat, rt, tex, warm);
            cam.transform.position = pos0;
            cam.transform.rotation = rot0;
            cam.fieldOfView = fov0;
            Debug.Log("IconShots: wrote " + Shots.Length + " shots");
        }

        /// <summary>Checks for the face expressions and the gold steamer skin (ICONSHOTS_EXTRA=1).</summary>
        private static void Extras(Bounds bb, Camera cam, Vector3 flat, RenderTexture rt, Texture2D tex, float warm)
        {
            var game = Squishy.Runtime.Game.SteamerGame.I;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var model = (Squishy.Runtime.Models.SquishyModel)typeof(Squishy.Runtime.Game.SteamerGame).GetField("pet", flags).GetValue(game);
            foreach (Squishy.Runtime.Models.SquishyModel.Mouth m in System.Enum.GetValues(typeof(Squishy.Runtime.Models.SquishyModel.Mouth)))
            {
                model.Held = false;
                model.Express(m, 5);
                model.Update(0, 0, false, 0);
                Shot(cam, rt, tex, bb, flat, 8, 1.5f, 30, warm, "face_" + m);
            }
            model.Held = true;
            model.Update(0, 0, false, 0);
            Shot(cam, rt, tex, bb, flat, 8, 1.5f, 30, warm, "face_Squished");
            model.Held = false;
            model.Update(0, 0, false, 0);
            var wall = (Squishy.Runtime.Models.SteamerModel)typeof(Squishy.Runtime.Game.SteamerGame).GetField("homeWall", flags).GetValue(game);
            var content = JsonUtility.FromJson<Squishy.Simulation.Game.GameContent>(File.ReadAllText("Assets/_Project/Resources/Content/game_content.json"));
            foreach (var sk in content.skins) if (sk.name == "Gold") wall.Skin(sk);
            Shot(cam, rt, tex, bb, flat, 42, 12f, 30, warm, "gold");
            // Every prestige accessory on the squishy, on one sheet (7 across), for checking.
            {
                const int C7 = 7, T2 = 256;
                int rows = (content.cosmetics.Length + C7 - 1) / C7;
                var small = new RenderTexture(T2, T2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var cos2 = new Texture2D(T2 * C7, T2 * rows, TextureFormat.RGBA32, false, false);
                model.SetFinish(content.finishes[8]);
                for (int i = 0; i < content.cosmetics.Length; i++)
                {
                    var cd = content.cosmetics[i];
                    model.SetCosmetics(cd.slot == "hat" ? cd : null, cd.slot == "face" ? cd : null, cd.slot == "neck" ? cd : null);
                    model.Express(Squishy.Runtime.Models.SquishyModel.Mouth.Smile, 5);
                    model.Update(0, 0, false, 0);
                    Shot(cam, rt, tex, bb, flat, 12, 1.05f, 30, warm, "_cell");
                    Graphics.Blit(rt, small);
                    var pa = RenderTexture.active;
                    RenderTexture.active = small;
                    cos2.ReadPixels(new Rect(0, 0, T2, T2), (i % C7) * T2, (rows - 1 - i / C7) * T2);
                    RenderTexture.active = pa;
                }
                cos2.Apply();
                File.WriteAllBytes("Library/IconChecks/cosmetics.png", cos2.EncodeToPNG());
                model.SetCosmetics(null, null, null);
            }
            // Tactile dents: three held presses on the pink squishy.
            model.SetFinish(content.finishes[8]);
            model.SetCosmetics(null, null, null);
            var held = new System.Collections.Generic.List<int>();
            foreach (var dir in new[] { new Vector3(-.35f, .55f, .75f), new Vector3(.3f, .7f, .6f), new Vector3(.05f, .92f, .35f) })
                held.Add(model.PressAt(model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(dir.normalized)), .055f));
            for (int i = 0; i < 20; i++) model.Update(.05f, 0, false, 0);
            Shot(cam, rt, tex, bb, flat, 22, 1.25f, 30, warm, "dents");
            {
                // A press on the face (the eye should sink with the skin), a two-finger squeeze, and draping over a stool.
                model.ReleaseAll();
                for (int i = 0; i < 80; i++) model.Update(.05f, 0, false, 0);
                int eyeDent = model.PressAt(model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(new Vector3(.36f, .1f, .93f).normalized)), .07f);
                for (int i = 0; i < 20; i++) model.Update(.05f, 0, false, 0);
                Shot(cam, rt, tex, model.Body.GetComponent<Renderer>().bounds, flat, 14, 1.5f, 30, warm, "face_dent");
                model.Release(eyeDent);
                for (int i = 0; i < 80; i++) model.Update(.05f, 0, false, 0);
                model.BeginPinch(model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(new Vector3(-1, .1f, .2f).normalized)), model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(new Vector3(1, .1f, .2f).normalized)));
                model.SetPinch(.9f);
                for (int i = 0; i < 20; i++) model.Update(.05f, 0, false, 0);
                Shot(cam, rt, tex, model.Body.GetComponent<Renderer>().bounds, flat, 14, 1.6f, 30, warm, "pinch");
                model.EndPinch();
                for (int i = 0; i < 100; i++) model.Update(.05f, 0, false, 0);
                model.BeginPinch(model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(new Vector3(-.3f, .5f, .8f).normalized)), model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(new Vector3(.3f, .5f, .8f).normalized)));
                model.SetPinch(.9f);
                for (int i = 0; i < 20; i++) model.Update(.05f, 0, false, 0);
                Shot(cam, rt, tex, model.Body.GetComponent<Renderer>().bounds, flat, 14, 1.6f, 30, warm, "pinch_small");
                model.EndPinch();
                for (int i = 0; i < 100; i++) model.Update(.05f, 0, false, 0);
                model.SetSupport(.45f, true);
                for (int i = 0; i < 30; i++) model.Update(.05f, 0, false, 0);
                Shot(cam, rt, tex, model.Body.GetComponent<Renderer>().bounds, flat, 4, 1.5f, 30, warm, "drape");
                model.SetSupport(0, false);
                for (int i = 0; i < 60; i++) model.Update(.05f, 0, false, 0);
            }
            foreach (int id in held) model.Release(id);
            for (int i = 0; i < 80; i++) model.Update(.05f, 0, false, 0); // let the dents rise
            model.SetStage(Squishy.Simulation.Game.GameRules.Life.Baby);
            model.Update(.05f, 0, false, 0);
            Shot(cam, rt, tex, bb, flat, 10, 1.25f, 30, warm, "baby");
            var side = Quaternion.AngleAxis(-55, Vector3.up) * flat;
            model.SetStage(Squishy.Simulation.Game.GameRules.Life.Adult);
            model.Update(.05f, 0, false, 0);
            Shot(cam, rt, tex, bb, side, 4, 1.25f, 30, warm, "leftside");

            // Glasses (each style) and every legendary squishy, close up.
            model.Express(Squishy.Runtime.Models.SquishyModel.Mouth.Smile, 5);
            foreach (var cos in content.cosmetics)
                if (cos.slot == "face" && cos.kind != "freckles" && cos.kind != "moustache")
                {
                    model.SetCosmetics(null, cos, null);
                    model.Update(0, 0, false, 0);
                    Shot(cam, rt, tex, bb, flat, 8, 1.4f, 30, warm, "glasses_" + cos.kind);
                }
            model.SetCosmetics(null, null, null);
            foreach (var fin in content.finishes)
                if (fin.tier == "Legendary" || fin.tier == "Pattern")
                {
                    model.SetFinish(fin);
                    model.Update(0, 0, false, 0);
                    Shot(cam, rt, tex, bb, flat, 14, 1.5f, 30, warm, "legendary_" + fin.name.Replace(" ", ""));
                }
            foreach (var fin in content.finishes)
                if (fin.clear)
                {
                    model.SetFinish(fin);
                    for (int i = 0; i < 30; i++) model.Update(.05f, 0, false, 0); // let the inside settle and swim a little
                    Shot(cam, rt, tex, bb, flat, 14, 1.5f, 30, warm, "clear_" + fin.name.Replace(" ", ""));
                }
            { var tubT = Squishy.Runtime.Game.Thumbs.Get("tub:aegean"); if (tubT != null) { var trt = new RenderTexture(tubT.width, tubT.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); Graphics.Blit(tubT, trt); var pa = RenderTexture.active; RenderTexture.active = trt; var tt = new Texture2D(tubT.width, tubT.height, TextureFormat.RGBA32, false, false); tt.ReadPixels(new Rect(0, 0, tubT.width, tubT.height), 0, 0); RenderTexture.active = pa; Directory.CreateDirectory("Library/IconChecks"); File.WriteAllBytes("Library/IconChecks/tub.png", tt.EncodeToPNG()); } }
            // Every style's plant on one sheet (6 x 4), rendered with the catalogue thumbnail camera.
            const int T = 256;
            var sheetRT = new RenderTexture(T, T, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var sheet = new Texture2D(T * 6, T * 4, TextureFormat.RGBA32, false, false);
            var fill = new Color32[T * 6 * T * 4];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(247, 240, 228, 255);
            sheet.SetPixels32(fill);
            for (int i = 0; i < content.styles.Length; i++)
            {
                var th = Squishy.Runtime.Game.Thumbs.Get("plant:" + content.styles[i].id);
                if (th == null) continue;
                Graphics.Blit(th, sheetRT);
                var prevA = RenderTexture.active;
                RenderTexture.active = sheetRT;
                var cell = new Texture2D(T, T, TextureFormat.RGBA32, false, false);
                cell.ReadPixels(new Rect(0, 0, T, T), 0, 0);
                RenderTexture.active = prevA;
                var px = cell.GetPixels32();
                int cx = (i % 6) * T, cy = (3 - i / 6) * T;
                for (int y = 0; y < T; y++)
                for (int x = 0; x < T; x++)
                {
                    var c = px[y * T + x];
                    if (c.a < 8) continue;
                    var d = sheet.GetPixel(cx + x, cy + y);
                    float a = c.a / 255f;
                    sheet.SetPixel(cx + x, cy + y, new Color(Mathf.Lerp(d.r, c.r / 255f, a), Mathf.Lerp(d.g, c.g / 255f, a), Mathf.Lerp(d.b, c.b / 255f, a), 1));
                }
            }
            sheet.Apply();
            Directory.CreateDirectory("Library/IconChecks");
            File.WriteAllBytes("Library/IconChecks/plants.png", sheet.EncodeToPNG());
            {
                var foods = new System.Collections.Generic.List<string>();
                for (int i = 0; i < content.pantry.Length; i++) foods.Add("food:" + i);
                ThumbSheet("foods", foods, 5);
                var fins = new System.Collections.Generic.List<string>();
                for (int i = 0; i < content.finishes.Length; i++) fins.Add("sq:" + i);
                ThumbSheet("finishes", fins, 8);
                var poms = new System.Collections.Generic.List<string>();
                for (int i = 0; i < 8 && i < content.styles.Length; i++) poms.Add("pomwand:" + content.styles[i].id);
                ThumbSheet("pompoms", poms, 4);
                var decor = new System.Collections.Generic.List<string>();
                foreach (var st in new[] { content.styles[0].id, content.styles[5].id }) foreach (var ty in new[] { "vase", "lantern", "sidetable", "easel", "pouf", "books" }) decor.Add(ty + ":" + st);
                ThumbSheet("decor", decor, 6);
            }

            {
            // Every style's shelf on one sheet2 (6 x 4), rendered with the catalogue thumbnail camera.
            const int T3 = 256;
            var sheetRT2 = new RenderTexture(T3, T3, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var sheet2 = new Texture2D(T3 * 6, T3 * 4, TextureFormat.RGBA32, false, false);
            var fill2 = new Color32[T3 * 6 * T3 * 4];
            for (int i = 0; i < fill2.Length; i++) fill2[i] = new Color32(247, 240, 228, 255);
            sheet2.SetPixels32(fill2);
            for (int i = 0; i < content.styles.Length; i++)
            {
                var th = Squishy.Runtime.Game.Thumbs.Get("shelf:" + content.styles[i].id);
                if (th == null) continue;
                Graphics.Blit(th, sheetRT2);
                var prevA = RenderTexture.active;
                RenderTexture.active = sheetRT2;
                var cell = new Texture2D(T3, T3, TextureFormat.RGBA32, false, false);
                cell.ReadPixels(new Rect(0, 0, T3, T3), 0, 0);
                RenderTexture.active = prevA;
                var px = cell.GetPixels32();
                int cx = (i % 6) * T3, cy = (3 - i / 6) * T3;
                for (int y = 0; y < T3; y++)
                for (int x = 0; x < T3; x++)
                {
                    var c = px[y * T3 + x];
                    if (c.a < 8) continue;
                    var d = sheet2.GetPixel(cx + x, cy + y);
                    float a = c.a / 255f;
                    sheet2.SetPixel(cx + x, cy + y, new Color(Mathf.Lerp(d.r, c.r / 255f, a), Mathf.Lerp(d.g, c.g / 255f, a), Mathf.Lerp(d.b, c.b / 255f, a), 1));
                }
            }
            sheet2.Apply();
            Directory.CreateDirectory("Library/IconChecks");
            File.WriteAllBytes("Library/IconChecks/shelves.png", sheet2.EncodeToPNG());
            }
        }

        /// <summary>A contact sheet of catalogue thumbnails (Library/IconChecks/NAME.png), for checking models by eye.</summary>
        private static void ThumbSheet(string name, System.Collections.Generic.List<string> keys, int cols)
        {
            const int T = 200;
            int rows = (keys.Count + cols - 1) / cols;
            var rt = new RenderTexture(T, T, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var sheet = new Texture2D(T * cols, T * rows, TextureFormat.RGBA32, false, false);
            var fill = new Color32[T * cols * T * rows];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(247, 240, 228, 255);
            sheet.SetPixels32(fill);
            for (int i = 0; i < keys.Count; i++)
            {
                var th = Squishy.Runtime.Game.Thumbs.Get(keys[i]);
                if (th == null) continue;
                Graphics.Blit(th, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var cell = new Texture2D(T, T, TextureFormat.RGBA32, false, false);
                cell.ReadPixels(new Rect(0, 0, T, T), 0, 0);
                RenderTexture.active = prev;
                var px = cell.GetPixels32();
                int cx = (i % cols) * T, cy = (rows - 1 - i / cols) * T;
                for (int y = 0; y < T; y++)
                for (int x = 0; x < T; x++)
                {
                    var c = px[y * T + x];
                    if (c.a < 8) continue;
                    var d = sheet.GetPixel(cx + x, cy + y);
                    float a = c.a / 255f;
                    sheet.SetPixel(cx + x, cy + y, new Color(Mathf.Lerp(d.r, c.r / 255f, a), Mathf.Lerp(d.g, c.g / 255f, a), Mathf.Lerp(d.b, c.b / 255f, a), 1));
                }
            }
            sheet.Apply();
            Directory.CreateDirectory("Library/IconChecks");
            File.WriteAllBytes("Library/IconChecks/" + name + ".png", sheet.EncodeToPNG());
        }

        private static void Shot(Camera cam, RenderTexture rt, Texture2D tex, Bounds bb, Vector3 flat, float elev, float fill, float fov, float warm, string name)
        {
            cam.fieldOfView = fov;
            float h = bb.size.y * fill, dist = h * .5f / Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
            var dir = Quaternion.AngleAxis(-elev, Vector3.Cross(Vector3.up, flat)) * flat;
            var target = bb.center + Vector3.up * bb.size.y * (fill > 4 ? 0 : .08f);
            cam.transform.position = target + dir.normalized * dist;
            cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
            var prevT = cam.targetTexture;
            float aspect = cam.aspect;
            cam.targetTexture = rt;
            cam.aspect = 1;
            Post.Set(cam.WorldToViewportPoint(bb.center).y, fill > 4 ? .16f : .13f, warm, 0);
            cam.Render();
            cam.targetTexture = prevT;
            cam.aspect = aspect;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory("Library/IconChecks");
            File.WriteAllBytes("Library/IconChecks/" + name + ".png", tex.EncodeToPNG()); // outside Assets: checks only
        }

        private static Transform FindPet()
        {
            return Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(t => t.name == "Squishy" && t.gameObject.activeInHierarchy && Path(t).Contains("/Home/"));
        }

        private static Bounds Bounds(Transform pet)
        {
            var rends = pet.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            var bb = rends[0].bounds;
            foreach (var r in rends) bb.Encapsulate(r.bounds);
            return bb;
        }

        /// <summary>Turns the squishy (its yaw node) so its face looks along <paramref name="dir"/>.</summary>
        private static void FaceTowards(Transform pet, Vector3 dir)
        {
            var yaw = pet.Find("yaw");
            var mouth = pet.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "mouth");
            if (yaw == null || mouth == null) return;
            System.Func<float> err = () =>
            {
                var fd = mouth.position - pet.position;
                fd.y = 0;
                return Vector3.SignedAngle(fd, dir, Vector3.up);
            };
            for (int i = 0; i < 4; i++)
            {
                float a = err();
                var before = yaw.localRotation;
                yaw.localRotation = Quaternion.AngleAxis(a, Vector3.up) * before;
                if (Mathf.Abs(err()) > Mathf.Abs(a)) yaw.localRotation = Quaternion.AngleAxis(-a, Vector3.up) * before;
            }
        }

        private static string Path(Transform t)
        {
            string p = "";
            for (; t != null; t = t.parent) p = "/" + t.name + p;
            return p + "/";
        }
    }
}
