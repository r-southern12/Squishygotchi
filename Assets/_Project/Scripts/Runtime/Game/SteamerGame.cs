using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Squishy.Runtime.Models;
using Squishy.Runtime.Save;
using Squishy.Runtime.Three;
using Squishy.Runtime.UI;
using Squishy.Runtime.World;
using Squishy.Simulation.Core;
using Squishy.Simulation.Game;
using Squishy.Simulation.Save;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Squishy.Runtime.Game.Ease;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// The whole game, a faithful port of reference/steamer-room.html: home room with the squishy and its care,
    /// arrange mode, kitchen, catalogue, shop, tasks and the separate unboxing counter.
    /// Rules live in <see cref="GameRules"/>; this class is the presentation and the frame loop.
    /// Split across partial files by area (Home, View, Unbox, Panels).
    /// </summary>
    public sealed partial class SteamerGame : MonoBehaviour
    {
        public const float Y0 = .19f, H = SteamerModel.H, R = SteamerModel.DefaultR, US = .42f;
        public const int HomeLayer = 8, UnboxLayer = 9, ThumbLayer = 10;

        public static SteamerGame I { get; private set; }

        private GameContent C;
        private GameRules Rules;
        private GameState S { get { return Rules.S; } }
        private SaveService _saves;
        private SaveData _save;

        private Camera cam;
        private Light keyLight;
        private Transform world, home, room, unbox;
        private Hud ui;
        private Sfx sfx;

        private string mode = "home";
        private float HR, FLOOR_R;
        private SteamerModel homeWall;
        private SteamerSkinData curSkin;
        private ParticlePool homeSteam, drops, unSteam, confetti;
        private readonly List<Item> items = new List<Item>();
        private List<Obstacle> obstacles = new List<Obstacle>();
        private float comfort;
        private string setBonus = "";
        private SquishyModel pet;
        private readonly Ai ai = new Ai();
        private readonly CamState camS = new CamState();
        private float timeScale = 1f, shake, flash, time, focusS = .5f, bandS = .12f;
        private int frameN;
        private bool reduce;
        private float quality = 1f, baseDpr = 1.75f, accT, accF, cool = 2;

        private void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            C = JsonUtility.FromJson<GameContent>(Resources.Load<TextAsset>("Content/game_content").text);
            C.Init();

            var clock = new SystemClock();
            _saves = new SaveService(new FileSaveStore(Path.Combine(Application.persistentDataPath, "save.json")), new JsonUtilitySaveSerializer(false), SaveMigrator.CreateDefault(), clock);
            LoadOutcome outcome;
            _save = _saves.Load(t => GameRules.NewState(C, (ulong)t), out outcome);
            Rules = new GameRules(C, _save.state);
            Rules.CoinsChanged += () => ui.SetCoins(S.coins);
            Rules.SteamersChanged += () => ui.SetSteamers(S.steamers, mode == "home");

            SetupRendering();
            sfx = gameObject.AddComponent<Sfx>();
            ui = new Hud(this, C);
            BuildHome();
            BuildUnbox();
            Thumbs.Init(this, C, Rules, world, keyLight);

            SetPet(S.favIdx);
            if (outcome == LoadOutcome.Loaded) CatchUp(_save.LastSavedUtc, clock.UtcNow);
            SetMode("home");
            RebuildObstacles();
            UpdatePity();
            ui.SetCoins(S.coins);
            ui.SetSteamers(S.steamers, true);
            ui.TaskDot(Rules.AnyTaskDone());
            DrawNeeds();
            if (S.dead) ResumeDead();
        }

        private void SetupRendering()
        {
            reduce = false;
            world = new GameObject("World (three.js space)").transform;
            world.localScale = new Vector3(1, 1, -1);
            home = Node.Group(world, "Home");
            unbox = Node.Group(world, "Unbox");

            cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.fieldOfView = 32;
            cam.nearClipPlane = .2f;
            cam.farClipPlane = 80;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowMSAA = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;

            keyLight = FindAnyObjectByType<Light>();
            if (keyLight == null) keyLight = new GameObject("Key").AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = ThreeMat.Hex("#FFE6C8");
            keyLight.intensity = 1.9f;
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowStrength = 1;
            keyLight.shadowBias = .02f;
            keyLight.shadowNormalBias = .2f;
            SceneLighting.Key(keyLight);
            SceneLighting.Globals();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.skybox = null;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.msaaSampleCount = 4;
                urp.supportsHDR = false;
                urp.shadowDistance = 16;
                urp.shadowCascadeCount = 1;
                urp.mainLightShadowmapResolution = 1024;
            }
            float css = Screen.width / 375f;
            baseDpr = Mathf.Min(css, 1.75f);
            ApplyQuality();
        }

        private void ApplyQuality()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.renderScale = Mathf.Clamp(baseDpr * quality / Mathf.Max(.01f, Screen.width / 375f), .1f, 2f);
        }

        // ---------------- frame ----------------

        private void Update()
        {
            float raw = Mathf.Max(0, Time.unscaledDeltaTime), dt = Mathf.Min(.05f, raw);
            time += dt;
            frameN++;
            Perf(raw);
            bool sheetOpen = ui.SheetOpen;
            if (sheetOpen) ui.PumpThumbs(2);
            Post.Sparkles(time, baseDpr * quality);
            shake = Mathf.Max(0, shake - dt * 1.8f);
            flash = Mathf.Max(0, flash - dt * 2.2f);
            float warm = 0;
            if (mode == "unbox") { StepUnbox(dt); warm = _warm; cam.backgroundColor = ThreeMat.Hex("#CDB48F"); cam.cullingMask = 1 << UnboxLayer; }
            else
            {
                StepHome(dt);
                HomeCamera(dt);
                homeSteam.Update(dt, true, cam);
                drops.Update(dt, true, cam);
                cam.backgroundColor = ThreeMat.Hex("#CFB38C");
                cam.cullingMask = 1 << HomeLayer;
            }
            if (mode == "unbox") { unSteam.Update(dt, true, cam); confetti.Update(dt, false, cam); }
            if (shake > 0 && !reduce)
            {
                float sh = shake * shake * .16f;
                cam.transform.position += Space3.U(Rnd(-sh, sh), Rnd(-sh, sh), 0);
            }
            if (mode == "home" && ui.BubbleOn) { var p = ScreenOf(PetWorld() + Vector3.up * (pet.Scale * 1.6f + .08f)); ui.PlaceBubble(p); }

            // Focus: keep the squishy and whatever it is using sharp.
            float f1, f2 = -1;
            if (mode != "unbox")
            {
                f1 = ViewportY(PetWorld() + Vector3.up * (pet.Scale * .6f));
                if (ai.target != null && mode == "home") f2 = ViewportY(ai.target.Pos + Vector3.up * .3f);
            }
            else if (ucam.kind == "closed") f1 = ViewportY(new Vector3(0, H * US * .6f, 0));
            else f1 = ViewportY(new Vector3(0, Y0 * US + ucam.half * .5f, 0));
            float focus = f1, band = mode == "edit" ? .6f : (mode == "unbox" && ucam.kind != "closed") ? .45f : .13f;
            if (f2 >= 0) { focus = (f1 + f2) / 2; band = Mathf.Max(band, Mathf.Abs(f1 - f2) / 2 + .08f); }
            focusS += (focus - focusS) * Mathf.Min(1, dt * 5);
            bandS += (band - bandS) * Mathf.Min(1, dt * 4);
            Post.Set(focusS, bandS, warm, reduce ? 0 : flash);
            cam.enabled = !sheetOpen || frameN % 6 == 0;
            ui.Update(dt);
        }

        private void Perf(float raw)
        {
            accT += raw;
            accF++;
            if (accT < 1) return;
            float ms = accT / accF * 1000;
            cool -= accT;
            ui.SetFps(Mathf.RoundToInt(1000 / ms) + " fps · " + Mathf.RoundToInt(quality * 100) + "% res");
            if (cool <= 0 && !ui.SheetOpen)
            {
                if (ms > 22 && quality > .5f) { quality = Mathf.Max(.5f, quality - .15f); ApplyQuality(); cool = 2; }
                else if (ms < 14 && quality < 1) { quality = Mathf.Min(1, quality + .1f); ApplyQuality(); cool = 4; }
            }
            accT = 0;
            accF = 0;
        }

        // ---------------- helpers ----------------

        private float ViewportY(Vector3 three) { return cam.WorldToViewportPoint(Space3.U(three)).y; }

        /// <summary>Screen position (UI panel pixels) of a three-space point.</summary>
        public Vector2 ScreenOf(Vector3 three)
        {
            var v = cam.WorldToViewportPoint(Space3.U(three));
            return new Vector2(v.x * ui.Width, (1 - v.y) * ui.Height);
        }

        private Vector3 PetWorld() { return pet.Pivot.localPosition; }

        private void Floater(string text, string cls = null)
        {
            if (mode == "unbox") return;
            ui.FloatAt(ScreenOf(PetWorld() + Vector3.up * (pet.Scale * 1.5f + .1f)), text, cls);
        }

        private void FloaterAt(Item it, string text, string cls = null) { ui.FloatAt(ScreenOf(it.Pos + Vector3.up * .7f), text, cls); }

        private void AddCoins(int n) { Rules.AddCoins(n); sfx.Coin(); }

        private bool Spend(int n)
        {
            if (!Rules.Spend(n)) { sfx.Bonk(); return false; }
            sfx.Coin();
            return true;
        }

        private void TaskEvent(string id, float n = 1)
        {
            if (S.dead) return;
            bool fin = Rules.TaskEvent(id, n);
            if (fin) { Floater("Task done!"); sfx.Chime(); }
            ui.TaskDot(Rules.AnyTaskDone());
            if (ui.PanelOpen("tasks") && (fin || UnityEngine.Random.value < .1f)) DrawTasks();
        }

        private static void Buzz(params int[] ms) { Haptics.Buzz(ms); }

        private void Later(float seconds, Action a) { StartCoroutine(After(seconds, a)); }

        private static IEnumerator After(float s, Action a)
        {
            yield return new WaitForSecondsRealtime(s);
            a();
        }

        private void SetMode(string m2)
        {
            mode = m2;
            ui.HideBubble();
            ui.SetMode(m2);
            if (m2 == "home") ui.SetHint("Tap furniture to send " + Rules.Fav.name + " there");
            if (m2 == "edit") { ui.SetHint("Drag items anywhere"); DrawTray(); }
            ui.SetSteamers(S.steamers, m2 == "home");
        }

        private void WipeTo(Action fn) { ui.Wipe(fn, reduce ? 0 : .4f); }

        // ---------------- save ----------------

        private void CatchUp(DateTime saved, DateTime now)
        {
            float away = (float)Math.Min((now - saved).TotalSeconds, 7 * 24 * 3600.0);
            if (away <= 1) return;
            ComputeComfort();
            for (float t = 0; t < away && !S.dead; t += 10)
            {
                float step = Mathf.Min(10, away - t);
                foreach (var it in items) if (it.arch == "plant") it.st.wilt = Mathf.Min(1, it.st.wilt + step * C.rules.plantWiltRate);
                if (Rules.StepCare(step, comfort)) S.dead = true;
            }
        }

        public void WriteSave()
        {
            if (_saves == null || _save == null) return;
            S.curSkin = Array.IndexOf(C.skins, curSkin);
            _saves.Save(_save);
        }

        private void OnApplicationPause(bool paused) { if (paused) WriteSave(); }
        private void OnApplicationQuit() { WriteSave(); }
    }
}
