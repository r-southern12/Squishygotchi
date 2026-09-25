using System.IO;
using Squishy.Data;
using Squishy.Runtime.Save;
using Squishy.Simulation.Core;
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
            Debug.Log("Save " + outcome + " (" + _store.Path + "): " + Save.coins + " coins, " + Save.steamers + " steamers.");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) WriteSave();
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
