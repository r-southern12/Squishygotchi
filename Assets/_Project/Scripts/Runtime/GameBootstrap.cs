using System;
using System.IO;
using Squishy.Data;
using Squishy.Runtime.Save;
using Squishy.Simulation.Care;
using Squishy.Simulation.Core;
using Squishy.Simulation.Room;
using Squishy.Simulation.Save;
using UnityEngine;

namespace Squishy.Runtime
{
    /// <summary>
    /// Entry point in the Main scene: sets frame rate, loads content and the save,
    /// and saves when the app is backgrounded or closed.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private ContentDatabase content;
        [SerializeField] private int targetFrameRate = 60;

        private SaveService _saves;
        private FileSaveStore _store;

        public static GameBootstrap Instance { get; private set; }
        public ContentDatabase Content { get { return content; } }
        public IClock Clock { get; private set; }
        public SaveData Save { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Mobile defaults to 30 fps unless asked.
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            if (content == null || content.economy == null)
            {
                Debug.LogError("GameBootstrap needs a ContentDatabase with an Economy asset. Run Squishy > Content > Seed From Spec.", this);
                enabled = false;
                return;
            }

            Clock = new SystemClock();
            _store = new FileSaveStore(Path.Combine(Application.persistentDataPath, "save.json"));
            _saves = new SaveService(_store, new JsonUtilitySaveSerializer(Debug.isDebugBuild), SaveMigrator.CreateDefault(), Clock);

            LoadOutcome outcome;
            Save = _saves.Load(content.economy.def, out outcome);
            if (outcome == LoadOutcome.Corrupt)
            {
                _store.WriteCorruptCopy(_saves.LastCorruptText);
                Debug.LogWarning("Save file was unreadable; started a new game and kept the old file as save.json.corrupt.");
            }
            if (content.care != null && RoomRules.EnsureStarterRoom(Save.pieces, content.care.starterRoom))
                Save.nextPieceId = Save.pieces.Count + 1;
            if (outcome == LoadOutcome.Loaded) CatchUp(Save.LastSavedUtc);
            Debug.Log("Save " + outcome + " (" + _store.Path + "): " + Save.coins + " coins, " + Save.steamers + " steamers.");
        }

        /// <summary>Game-speed multiplier for testing (Home scene's F key). Always 1 in release play.</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>Comfort from the pieces placed in the room.</summary>
        public int Comfort()
        {
            return RoomRules.Comfort(Save.pieces, id =>
            {
                var t = content.ItemType(id);
                return t != null ? t.def : null;
            }, content.economy.def);
        }

        public float ComfortSlowdown()
        {
            return CareSim.ComfortSlowdown(Comfort(), content.economy.def);
        }

        /// <summary>Simulates the time the app was closed or in the background.</summary>
        private void CatchUp(DateTime sinceUtc)
        {
            if (content.care == null) return;
            double away = (Clock.UtcNow - sinceUtc).TotalSeconds;
            if (away < 1) return;
            bool died = CareSim.SimulateOffline(Save.care, content.care.care, away, ComfortSlowdown());
            Debug.Log("Caught up " + (int)away + "s away" + (died ? " (the squishy died while you were away)." : "."));
        }

        private void Update()
        {
            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer >= AutosaveSeconds)
            {
                _autosaveTimer = 0f;
                WriteSave();
            }
        }

        private const float AutosaveSeconds = 30f;
        private float _autosaveTimer;
        private DateTime _pausedAtUtc;

        private void OnApplicationPause(bool paused)
        {
            if (Save == null) return;
            if (paused)
            {
                _pausedAtUtc = Clock.UtcNow;
                WriteSave();
            }
            else if (_pausedAtUtc != default(DateTime))
            {
                CatchUp(_pausedAtUtc);
            }
        }

        private void OnApplicationQuit()
        {
            WriteSave();
        }

        public void WriteSave()
        {
            if (_saves != null && Save != null) _saves.Save(Save);
        }
    }
}
