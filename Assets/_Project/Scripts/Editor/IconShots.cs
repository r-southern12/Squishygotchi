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
            if (f == 20 && Squishy.Runtime.Game.SteamerGame.I != null)
            {
                // A fresh save starts on the first-run (empty room, first steamer): checks want the furnished room.
                var g0 = Squishy.Runtime.Game.SteamerGame.I;
                var fl = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var r0 = (Squishy.Simulation.Game.GameRules)typeof(Squishy.Runtime.Game.SteamerGame).GetField("Rules", fl).GetValue(g0);
                if (r0.S.intro > 0) { r0.S.intro = 0; typeof(Squishy.Runtime.Game.SteamerGame).GetMethod("RebuildHome", fl).Invoke(g0, null); }
            }
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
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_NOTIF") == "1")
            {
                if (f < 60) return;
                EditorApplication.update -= Tick;
                var game = Squishy.Runtime.Game.SteamerGame.I;
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var rules = (Squishy.Simulation.Game.GameRules)typeof(Squishy.Runtime.Game.SteamerGame).GetField("Rules", flags).GetValue(game);
                var png = Squishy.Runtime.Game.SquishyArt.Png(rules.Fav, Squishy.Runtime.Game.SquishyArt.Mood.Droopy, rules.LifeStage());
                var scene = (byte[])typeof(Squishy.Runtime.Game.Notifier).GetMethod("Scene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { png, new System.Collections.Generic.List<int> { 0, 1, 3 }, rules });
                Directory.CreateDirectory(Dir);
                File.WriteAllBytes(Dir + "/notif.png", scene);
                var happyPng = Squishy.Runtime.Game.SquishyArt.Png(rules.Fav, Squishy.Runtime.Game.SquishyArt.Mood.Happy, rules.LifeStage());
                File.WriteAllBytes(Dir + "/notif_morning.png", (byte[])typeof(Squishy.Runtime.Game.Notifier).GetMethod("Scene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { happyPng, new System.Collections.Generic.List<int> { 4 }, rules }));
                Finish(0);
                return;
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_REACH") == "1")
            {
                // Ask for every piece in the room in turn: which can it reach (walk) and which does it give up on.
                EditorApplication.update -= Tick;
                var game = Squishy.Runtime.Game.SteamerGame.I;
                var fl = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var T = typeof(Squishy.Runtime.Game.SteamerGame);
                var aiObj = T.GetField("ai", fl).GetValue(game);
                var items = (System.Collections.IList)T.GetField("items", fl).GetValue(game);
                var rr = (Squishy.Simulation.Game.GameRules)T.GetField("Rules", fl).GetValue(game);
                rr.S.asleep = false; rr.S.tucked = false;
                var sb = new System.Text.StringBuilder("REACH:");
                foreach (var it in items)
                {
                    aiObj.GetType().GetField("mode").SetValue(aiObj, "idle");
                    aiObj.GetType().GetField("act").SetValue(aiObj, null);
                    try { T.GetMethod("UseItem", fl).Invoke(game, new object[] { it, true, null, null }); }
                    catch (System.Exception e) { sb.Append(" [ERR " + e.InnerException?.Message + "]"); }
                    string arch = (string)it.GetType().GetProperty("arch").GetValue(it);
                    string mode = (string)aiObj.GetType().GetField("mode").GetValue(aiObj);
                    var act = aiObj.GetType().GetField("act").GetValue(aiObj);
                    sb.Append(" " + arch + "=" + (act == null ? "none" : mode));
                }
                Debug.Log(sb.ToString());
                Debug.Log(game.NavSelfCheck(200));
                Finish(0);
                return;
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_FOODS") == "1")
            {
                // Every ingredient model at its own (true) size in a row, plus their measured sizes, to set display sizes.
                EditorApplication.update -= Tick;
                var game = Squishy.Runtime.Game.SteamerGame.I;
                var fl = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var fc = (Squishy.Simulation.Game.GameContent)typeof(Squishy.Runtime.Game.SteamerGame).GetField("C", fl).GetValue(game);
                var row = new GameObject("foodRow").transform;
                row.position = new Vector3(0, 20, 0);
                var sb = new System.Text.StringBuilder("FoodSizes:");
                float x = 0;
                for (int i = 0; i < fc.pantry.Length; i++)
                {
                    var fo = Squishy.Runtime.Models.KitchenModels.Food(fc, i, row);
                    var b = Squishy.Runtime.Three.Node.LocalBounds(fo, row);
                    float w = Mathf.Max(b.size.x, b.size.z);
                    if (System.Environment.GetEnvironmentVariable("ICONSHOTS_REVEAL") == "1")
                    {
                        // As the steamer reveal shows it: filling the plate (1.6 wide, 1.3 tall), then its own size.
                        float k = Mathf.Min(1.6f / Mathf.Max(.01f, w), 1.3f / Mathf.Max(.01f, b.size.y)) * (fc.pantry[i].size > 0 ? fc.pantry[i].size : 1);
                        fo.localScale *= k;
                        b = Squishy.Runtime.Three.Node.LocalBounds(fo, row);
                        w = Mathf.Max(b.size.x, b.size.z);
                    }
                    sb.Append(" " + i + ":" + fc.pantry[i].name + "=" + w.ToString("F3") + "x" + b.size.y.ToString("F3"));
                    fo.localPosition += new Vector3(x + w / 2 - b.center.x, -b.min.y, -b.center.z);
                    x += w + .04f;
                    Squishy.Runtime.Three.Node.SetLayer(fo, Camera.main.gameObject.layer);
                }
                Debug.Log(sb.ToString());
                var cam = Camera.main;
                foreach (Transform t in row) Squishy.Runtime.Three.Node.SetLayer(t, 0);
                cam.cullingMask = 1;
                var rt = new RenderTexture(2400, 400, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                cam.targetTexture = rt;
                cam.aspect = 6;
                cam.fieldOfView = 20;
                var centre = row.position + new Vector3(x / 2, .08f, 0);
                cam.transform.position = centre + new Vector3(0, .25f, -1) * (x * .52f / Mathf.Tan(10 * Mathf.Deg2Rad) / 6 * 1.4f);
                cam.transform.LookAt(centre);
                cam.Render();
                var tex = new Texture2D(2400, 400, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 2400, 400), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory(Dir);
                File.WriteAllBytes(Dir + "/foods_row.png", tex.EncodeToPNG());
                Finish(0);
                return;
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_SHOWER") == "1")
            {
                // A shower in the room: photographed with clear glass, then steamed up mid-shower.
                var game = Squishy.Runtime.Game.SteamerGame.I;
                var fl = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var T = typeof(Squishy.Runtime.Game.SteamerGame);
                var rr = (Squishy.Simulation.Game.GameRules)T.GetField("Rules", fl).GetValue(game);
                if (f == 40) { rr.S.asleep = false; rr.S.tucked = false; rr.S.items.Add(new Squishy.Simulation.Game.PieceState { key = "shower:aegean", x = .55f, z = -.3f }); T.GetMethod("RebuildHome", fl).Invoke(game, null); return; }
                if (f < 60) return;
                Squishy.Runtime.Game.Item sh = null;
                foreach (Squishy.Runtime.Game.Item it in (System.Collections.IList)T.GetField("items", fl).GetValue(game)) if (it.a.id == "shower") sh = it;
                if (sh == null) { Debug.Log("SHOWERCHK no shower"); EditorApplication.update -= Tick; Finish(1); return; }
                var cam = Camera.main;
                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
                var flat = cam.transform.position - sh.g.position;
                flat.y = 0;
                Directory.CreateDirectory(Dir);
                if (f == 60) { Shot(cam, rt, tex, Bounds(sh.g), flat.normalized, 22, 1.25f, 30, Shader.GetGlobalFloat("_PostWarm"), "shower_clear"); T.GetMethod("UseItem", fl).Invoke(game, new object[] { sh, true, null, null }); return; }
                var aiObj = T.GetField("ai", fl).GetValue(game);
                string mode = (string)aiObj.GetType().GetField("mode").GetValue(aiObj);
                float actT = (float)aiObj.GetType().GetField("actT").GetValue(aiObj);
                if ((mode == "act" && actT > 3f) || f > 2000)
                {
                    EditorApplication.update -= Tick;
                    Debug.Log("SHOWERCHK mode=" + mode + " actT=" + actT + " steam=" + sh.parts.steam);
                    Shot(cam, rt, tex, Bounds(sh.g), flat.normalized, 22, 1.25f, 30, Shader.GetGlobalFloat("_PostWarm"), "shower_steam");
                    Finish(0);
                }
                return;
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_BUBBLES") == "1")
            {
                // A cloud of bath bubbles beside the squishy, photographed mid-rise (to check how they read).
                var game = Squishy.Runtime.Game.SteamerGame.I;
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var T = typeof(Squishy.Runtime.Game.SteamerGame);
                var pool = (Squishy.Runtime.World.ParticlePool)T.GetField("bathBubbles", flags).GetValue(game);
                var pw = (Vector3)T.GetMethod("PetWorld", flags).Invoke(game, null);
                if (f < 260) { if (f % 3 == 0) pool.Spawn(pw + new Vector3(Random.Range(-.35f, .35f), Random.Range(.05f, .3f), Random.Range(-.2f, .2f)), new Vector3(0, Random.Range(.08f, .2f), 0), Random.Range(.03f, .06f), 3f, 1.5f, .03f); return; }
                EditorApplication.update -= Tick;
                var cam = Camera.main;
                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
                var flat = cam.transform.position - Bounds(FindPet()).center;
                flat.y = 0;
                Directory.CreateDirectory(Dir);
                Shot(cam, rt, tex, Bounds(FindPet()), flat.normalized, 20, 2.6f, 30, Shader.GetGlobalFloat("_PostWarm"), "bubbles");
                var bt = Squishy.Runtime.Three.Textures.Bubble();
                var brt = new RenderTexture(bt.width * 3, bt.height * 3, 0, RenderTextureFormat.ARGB32);
                var prevA = RenderTexture.active;
                RenderTexture.active = brt; GL.Clear(true, true, new Color(.2f, .55f, .62f, 1)); RenderTexture.active = prevA;
                Graphics.Blit(bt, brt, new Material(Shader.Find("Hidden/BlitCopy")) { });
                var mat = new Material(Shader.Find("Sprites/Default"));
                RenderTexture.active = brt; GL.Clear(true, true, new Color(.55f, .78f, .82f, 1)); GL.PushMatrix(); GL.LoadPixelMatrix(0, brt.width, brt.height, 0); Graphics.DrawTexture(new Rect(0, 0, brt.width, brt.height), bt, mat); GL.PopMatrix();
                var btex = new Texture2D(brt.width, brt.height, TextureFormat.RGBA32, false); btex.ReadPixels(new Rect(0, 0, brt.width, brt.height), 0, 0); btex.Apply(); RenderTexture.active = prevA;
                File.WriteAllBytes(Dir + "/bubble_tex.png", btex.EncodeToPNG());
                Finish(0);
                return;
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_EDIT") == "1")
            {
                // Press Arrange from code in a few situations and log what happens (mode, and any error).
                var game = Squishy.Runtime.Game.SteamerGame.I;
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
                var T = typeof(Squishy.Runtime.Game.SteamerGame);
                var rules = (Squishy.Simulation.Game.GameRules)T.GetField("Rules", flags).GetValue(game);
                if (f == 100) { Debug.Log("EDITCHK intro=" + rules.S.intro + " mode=" + T.GetField("mode", flags).GetValue(game)); rules.S.intro = 0; T.GetMethod("RebuildHome", flags).Invoke(game, null); return; }
                if (f == 140 || f == 200 || f == 260)
                {
                    try
                    {
                        if (f == 200) { T.GetMethod("GoToBed", flags).Invoke(game, null); }
                        if (f == 260) { var ui = T.GetField("ui", flags).GetValue(game); }
                        game.EnterEdit();
                        Debug.Log("EDITCHK f" + f + " after EnterEdit mode=" + T.GetField("mode", flags).GetValue(game));
                        T.GetMethod("ExitEdit", flags).Invoke(game, null);
                        Debug.Log("EDITCHK f" + f + " after ExitEdit mode=" + T.GetField("mode", flags).GetValue(game));
                    }
                    catch (System.Exception e) { Debug.Log("EDITCHK f" + f + " ERROR " + e); }
                    return;
                }
                if (f < 300) return;
                EditorApplication.update -= Tick;
                Finish(0);
                return;
            }
            if (System.Environment.GetEnvironmentVariable("ICONSHOTS_COS") == "1")
            {
                // Every prestige accessory on the squishy: a front sheet and a three-quarter sheet (7 across).
                var game = Squishy.Runtime.Game.SteamerGame.I;
                if (f < 150) return;
                if (f == 150) { game.enabled = false; return; } // stand still
                EditorApplication.update -= Tick;
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var T = typeof(Squishy.Runtime.Game.SteamerGame);
                var petM = (Squishy.Runtime.Models.SquishyModel)T.GetField("pet", flags).GetValue(game);
                var content = (Squishy.Simulation.Game.GameContent)T.GetField("C", flags | System.Reflection.BindingFlags.Public).GetValue(game);
                var cam = Camera.main;
                var cpet = FindPet();
                petM.SetCosmetics(null, null, null);
                petM.SetFinish(content.finishes[System.Environment.GetEnvironmentVariable("ICONSHOTS_FIN") != null ? int.Parse(System.Environment.GetEnvironmentVariable("ICONSHOTS_FIN")) : 0]);
                petM.Express(Squishy.Runtime.Models.SquishyModel.Mouth.Smile, 5);
                petM.Update(0, 0, false, 0);
                var bb0 = Bounds(cpet);
                var flat = cam.transform.position - bb0.center;
                flat.y = 0;
                flat.Normalize();
                FaceTowards(cpet, flat);
                bb0 = Bounds(cpet);
                bb0.center += Vector3.up * bb0.size.y * .25f; // room above for hats
                float warm = Shader.GetGlobalFloat("_PostWarm");
                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
                Directory.CreateDirectory(Dir);
                const int C7 = 7, T2 = 300;
                var only = System.Environment.GetEnvironmentVariable("ICONSHOTS_ONLY");
                var list = content.cosmetics.Where(x => string.IsNullOrEmpty(only) || only.Split(',').Contains(x.id)).ToArray();
                int rows = (list.Length + C7 - 1) / C7;
                var small = new RenderTexture(T2, T2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Shot(cam, rt, tex, bb0, flat, 8, 1.9f, 30, warm, "_cell"); // warm-up: the first render came out off-colour
                foreach (var view in new[] { ("front", 0f, 8f), ("side", 50f, 22f) })
                {
                    var dir = Quaternion.AngleAxis(view.Item2, Vector3.up) * flat;
                    var sheet = new Texture2D(T2 * C7, T2 * rows, TextureFormat.RGBA32, false, false);
                    for (int i = 0; i < list.Length; i++)
                    {
                        var cd = list[i];
                        petM.SetCosmetics(cd.slot == "hat" ? cd : null, cd.slot == "face" ? cd : null, cd.slot == "neck" ? cd : null);
                        petM.Update(0, 0, false, 0);
                        var bbc = bb0;
                        if (cd.slot == "neck") bbc.center -= Vector3.up * bb0.size.y * .45f;
                        Shot(cam, rt, tex, bbc, dir, view.Item3, System.Environment.GetEnvironmentVariable("ICONSHOTS_FILL") != null ? float.Parse(System.Environment.GetEnvironmentVariable("ICONSHOTS_FILL"), System.Globalization.CultureInfo.InvariantCulture) : 1.9f, 30, warm, "_cell");
                        Graphics.Blit(rt, small);
                        var pa = RenderTexture.active;
                        RenderTexture.active = small;
                        sheet.ReadPixels(new Rect(0, 0, T2, T2), (i % C7) * T2, (rows - 1 - i / C7) * T2);
                        RenderTexture.active = pa;
                    }
                    sheet.Apply();
                    File.WriteAllBytes("Library/IconChecks/cos_" + view.Item1 + ".png", sheet.EncodeToPNG());
                }
                Debug.Log("IconShots: cosmetics " + string.Join(", ", list.Select(x => x.id)));
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
                // The new things to do: play together, pamper, a sticker (then the photo once they have run).
                if (f - _inBath == 10) { T.GetMethod("VisitPlay", flags).Invoke(game, null); T.GetMethod("VisitPamper", flags).Invoke(game, null); T.GetMethod("PressSticker", flags).Invoke(game, new object[] { "star" }); }
                if (f - _inBath == 40) Debug.Log("IconShots: visit extras buddy=" + (T.GetField("buddy", flags).GetValue(game) != null) + " pamperT=" + T.GetField("pamperT", flags).GetValue(game) + " playT=" + T.GetField("bPlayT", flags).GetValue(game));
                if (f - _inBath < 120) return;
                EditorApplication.update -= Tick;
                Debug.Log("IconShots: visit done=" + string.Join(",", (System.Collections.Generic.HashSet<string>)T.GetField("visitDone", flags).GetValue(game)));
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
                            typeof(Squishy.Runtime.Game.SteamerGame).GetMethod("UseItem", flags).Invoke(game, new object[] { it, true, null, null });
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
            Debug.Log(game.NavSelfCheck(200));
            {
                // Each ingredient model's own size (before the steamer plate stretches it), to set display sizes.
                var fcontent = (Squishy.Simulation.Game.GameContent)typeof(Squishy.Runtime.Game.SteamerGame).GetField("C", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(game);
                var tmp = new GameObject("foodSizes").transform;
                var sb = new System.Text.StringBuilder("FoodSizes:");
                for (int i = 0; i < fcontent.pantry.Length; i++)
                {
                    var fo = Squishy.Runtime.Models.KitchenModels.Food(fcontent, i, tmp);
                    var b = Squishy.Runtime.Three.Node.LocalBounds(fo, tmp);
                    sb.Append(" " + fcontent.pantry[i].name + "=" + Mathf.Max(b.size.x, b.size.z).ToString("F3") + "x" + b.size.y.ToString("F3"));
                    Object.DestroyImmediate(fo.gameObject);
                }
                Object.DestroyImmediate(tmp.gameObject);
                Debug.Log(sb.ToString());
            }
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
                // A second pinch straight after (higher up): the first keeps its shape and relaxes slowly, both show.
                for (int i = 0; i < 4; i++) model.Update(.05f, 0, false, 0);
                model.BeginPinch(model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(new Vector3(-.35f, .62f, .7f).normalized)), model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(new Vector3(.35f, .62f, .7f).normalized)));
                model.SetPinch(.9f);
                for (int i = 0; i < 12; i++) model.Update(.05f, 0, false, 0);
                Shot(cam, rt, tex, model.Body.GetComponent<Renderer>().bounds, flat, 14, 1.6f, 30, warm, "pinch_double");
                model.EndPinch();
                for (int i = 0; i < 100; i++) model.Update(.05f, 0, false, 0);
                model.BeginPinch(model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(new Vector3(-.3f, .5f, .8f).normalized)), model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(new Vector3(.3f, .5f, .8f).normalized)));
                model.SetPinch(.9f);
                for (int i = 0; i < 20; i++) model.Update(.05f, 0, false, 0);
                Shot(cam, rt, tex, model.Body.GetComponent<Renderer>().bounds, flat, 14, 1.6f, 30, warm, "pinch_small");
                model.EndPinch();
                for (int i = 0; i < 100; i++) model.Update(.05f, 0, false, 0);
                // Fingers placed close together (the usual pinch): it should still pinch up a clear fold.
                model.BeginPinch(model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(new Vector3(-.09f, .45f, .88f).normalized)), model.Body.TransformPoint(Squishy.Runtime.Models.SquishyModel.ShapeAt(new Vector3(.09f, .45f, .88f).normalized)));
                model.SetPinch(.9f);
                for (int i = 0; i < 20; i++) model.Update(.05f, 0, false, 0);
                Shot(cam, rt, tex, model.Body.GetComponent<Renderer>().bounds, flat, 14, 1.6f, 30, warm, "pinch_tiny");
                // The same pinch dragged sideways while held (as fingers do): it should nudge, not pull out a flap.
                model.DragPinch(Vector3.ClampMagnitude(new Vector3(model.Scale * model.StageScale * .4f, 0, 0), model.Scale * model.StageScale * .25f) * .6f);
                for (int i = 0; i < 20; i++) model.Update(.05f, 0, false, 0);
                Shot(cam, rt, tex, model.Body.GetComponent<Renderer>().bounds, flat, 14, 1.6f, 30, warm, "pinch_drag");
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
                var tools = new System.Collections.Generic.List<string>();
                for (int i = 0; i < content.tools.Length; i++) for (int j = 0; j < content.toolSkins.Length; j++) tools.Add("tskin:" + i + ":" + j);
                ThumbSheet("tools", tools, content.toolSkins.Length);
                var tea = new System.Collections.Generic.List<string>();
                for (int i = 0; i < 4 && i < content.styles.Length; i++) tea.Add("teatable:" + content.styles[i * 5 % content.styles.Length].id);
                ThumbSheet("tea", tea, 2);
                var skinKeys = new System.Collections.Generic.List<string>();
                for (int i = 0; i < content.skins.Length; i++) skinKeys.Add("skin:" + i);
                ThumbSheet("skins", skinKeys, 5);
                // Mid-pour close-ups from three sides (512 px each): the tipped spout should end over the cup.
                Squishy.Runtime.Models.ItemModels.PreviewPour = 1;
                var live = Squishy.Runtime.Game.Thumbs.LiveBegin("teatable:" + content.styles[1].id);
                if (live != null)
                {
                    var pourSheet = new Texture2D(512 * 3, 512, TextureFormat.RGBA32, false, false);
                    var srgb = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    for (int v = 0; v < 3; v++)
                    {
                        if (v > 0) Squishy.Runtime.Game.Thumbs.LiveStep(2.1f / .7f);
                        Graphics.Blit(live, srgb);
                        var pa = RenderTexture.active;
                        RenderTexture.active = srgb;
                        pourSheet.ReadPixels(new Rect(0, 0, 512, 512), v * 512, 0);
                        RenderTexture.active = pa;
                    }
                    pourSheet.Apply();
                    File.WriteAllBytes("Library/IconChecks/pour.png", pourSheet.EncodeToPNG());
                    Squishy.Runtime.Game.Thumbs.LiveEnd();
                }
                Squishy.Runtime.Models.ItemModels.PreviewPour = -1;
                // The Open 10 live 3D view of a squishy (it was small and blurry): saved at full size.
                var liveSq = Squishy.Runtime.Game.Thumbs.LiveBegin("sq:57");
                if (liveSq != null)
                {
                    var srgb2 = new RenderTexture(liveSq.width, liveSq.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(liveSq, srgb2);
                    var pa2 = RenderTexture.active;
                    RenderTexture.active = srgb2;
                    var t2 = new Texture2D(liveSq.width, liveSq.height, TextureFormat.RGBA32, false, false);
                    t2.ReadPixels(new Rect(0, 0, liveSq.width, liveSq.height), 0, 0);
                    RenderTexture.active = pa2;
                    File.WriteAllBytes("Library/IconChecks/live_sq.png", t2.EncodeToPNG());
                    Squishy.Runtime.Game.Thumbs.LiveEnd();
                }
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
