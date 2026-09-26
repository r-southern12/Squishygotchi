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
        private const string Key = "Squishy.IconShots", Dir = "Assets/_Project/Art/Icon";
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
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
            EditorApplication.EnterPlaymode();
        }

        private static int _start = -1;
        private static float _ground = float.MaxValue, _lastY = float.MaxValue;

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (_start < 0) _start = Time.frameCount;
            int f = Time.frameCount - _start;
            if (f > 4000) Finish(1);
            if (f < 180) return; // let the room build, the squishy settle and thumbnails finish
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
            Directory.CreateDirectory("Temp/IconChecks");
            File.WriteAllBytes("Temp/IconChecks/" + name + ".png", tex.EncodeToPNG()); // outside Assets: checks only
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
