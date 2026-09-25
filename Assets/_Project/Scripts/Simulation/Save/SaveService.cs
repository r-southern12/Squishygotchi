using System;
using Squishy.Simulation.Content;
using Squishy.Simulation.Core;

namespace Squishy.Simulation.Save
{
    /// <summary>Where save text lives: a local file today, cloud storage later.</summary>
    public interface ISaveStore
    {
        bool TryRead(out string text);
        void Write(string text);
    }

    public interface ISaveSerializer
    {
        string Serialize(SaveData data);
        SaveData Deserialize(string text);
    }

    public enum LoadOutcome
    {
        NewGame,
        Loaded,
        /// <summary>The save couldn't be read. A new game was started and the old text kept aside.</summary>
        Corrupt,
    }

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

        /// <summary>Text of a save that failed to load, so it can be backed up rather than lost.</summary>
        public string LastCorruptText { get; private set; }

        public SaveData Load(EconomyDef economy, out LoadOutcome outcome)
        {
            LastCorruptText = null;
            string text;
            if (!_store.TryRead(out text) || string.IsNullOrEmpty(text))
            {
                outcome = LoadOutcome.NewGame;
                return NewGame(economy);
            }

            SaveData data;
            try
            {
                data = _serializer.Deserialize(text);
            }
            catch (Exception)
            {
                data = null;
            }

            if (data == null)
            {
                LastCorruptText = text;
                outcome = LoadOutcome.Corrupt;
                return NewGame(economy);
            }

            // A save from a newer build throws SaveVersionException on purpose: never overwrite it.
            _migrator.Migrate(data);
            outcome = LoadOutcome.Loaded;
            return data;
        }

        public void Save(SaveData data)
        {
            data.version = SaveMigrator.CurrentVersion;
            data.lastSavedUtcTicks = _clock.UtcNow.Ticks;
            _store.Write(_serializer.Serialize(data));
        }

        public SaveData NewGame(EconomyDef economy)
        {
            long now = _clock.UtcNow.Ticks;
            var data = new SaveData();
            data.version = SaveMigrator.CurrentVersion;
            data.createdUtcTicks = now;
            data.lastSavedUtcTicks = now;
            data.coins = economy.startingCoins;
            data.steamers = economy.startingSteamers;
            data.roomLevel = economy.startingRoomLevel;
            if (!string.IsNullOrEmpty(economy.startingSquishyId))
            {
                data.favouriteSquishyId = economy.startingSquishyId;
                data.squishies.Add(new OwnedCount(economy.startingSquishyId, 1));
            }
            // Seed from the clock so each new game gets its own sequence; the state is then saved.
            data.gachaRngState = new Pcg32((ulong)now).State;
            return data;
        }
    }
}
