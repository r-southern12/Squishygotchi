using System;
using Squishy.Simulation.Game;

namespace Squishy.Simulation.Save
{
    /// <summary>
    /// The save file: a version, timestamps (needs drain from lastSavedUtcTicks on resume) and the game state.
    /// When fields change meaning, bump <see cref="SaveMigrator.CurrentVersion"/> and add a migration.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version;
        public long createdUtcTicks;
        public long lastSavedUtcTicks;
        public GameState state;

        public DateTime LastSavedUtc { get { return new DateTime(lastSavedUtcTicks, DateTimeKind.Utc); } }
    }
}
