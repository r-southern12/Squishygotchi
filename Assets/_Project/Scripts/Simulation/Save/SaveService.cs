using System;
using Squishy.Simulation.Core;

namespace Squishy.Simulation.Save
{
    public interface ISaveStore
    {
        bool TryRead(out string text);
        void Write(string text);
        void WriteCorruptCopy(string text);
    }

    public interface ISaveSerializer
    {
        string Serialize(SaveData data);
        SaveData Deserialize(string text);
    }

    public enum LoadOutcome { NewGame, Loaded, Corrupt }

    /// <summary>Loads, migrates and writes the save. A missing, corrupt or pre-port save starts a new game.</summary>
    public sealed class SaveService
    {
        private readonly ISaveStore _store;
        private readonly ISaveSerializer _serializer;
        private readonly SaveMigrator _migrator;
        private readonly IClock _clock;

        public SaveService(ISaveStore store, ISaveSerializer serializer, SaveMigrator migrator, IClock clock)
        {
            _store = store;
            _serializer = serializer;
            _migrator = migrator;
            _clock = clock;
        }

        public SaveData Load(Func<long, Game.GameState> newState, out LoadOutcome outcome)
        {
            string text;
            if (!_store.TryRead(out text) || string.IsNullOrEmpty(text)) { outcome = LoadOutcome.NewGame; return NewGame(newState); }
            SaveData data;
            try { data = _serializer.Deserialize(text); }
            catch (Exception) { data = null; }
            if (data == null) { _store.WriteCorruptCopy(text); outcome = LoadOutcome.Corrupt; return NewGame(newState); }
            _migrator.Migrate(data); // a save from a newer build throws: never overwrite it
            if (data.state == null) { outcome = LoadOutcome.NewGame; return NewGame(newState); }
            outcome = LoadOutcome.Loaded;
            return data;
        }

        public void Save(SaveData data)
        {
            data.version = SaveMigrator.CurrentVersion;
            data.lastSavedUtcTicks = _clock.UtcNow.Ticks;
            _store.Write(_serializer.Serialize(data));
        }

        public SaveData NewGame(Func<long, Game.GameState> newState)
        {
            long now = _clock.UtcNow.Ticks;
            return new SaveData { version = SaveMigrator.CurrentVersion, createdUtcTicks = now, lastSavedUtcTicks = now, state = newState(now) };
        }
    }
}
