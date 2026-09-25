using System;
using System.Collections.Generic;
using Squishy.Simulation.Care;
using Squishy.Simulation.Gacha;
using Squishy.Simulation.Room;

namespace Squishy.Simulation.Save
{
    /// <summary>
    /// Everything that persists between sessions. Plain fields only, so any serializer
    /// (Unity's JsonUtility today, a cloud format later) can handle it.
    /// When you add or change a field: bump <see cref="SaveMigrator.CurrentVersion"/> and add
    /// a migration if old saves need converting.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version;

        /// <summary>UTC ticks. Needs drain is computed from lastSavedUtcTicks on resume.</summary>
        public long createdUtcTicks;
        public long lastSavedUtcTicks;

        // Currencies
        public int coins;
        public int steamers;
        public int prestige;

        // Gacha: RNG state and pity counters travel with the save so results stay deterministic.
        public ulong gachaRngState;
        public PityState pity = new PityState();

        // Collection (ids from content data)
        public string favouriteSquishyId;
        public List<OwnedCount> squishies = new List<OwnedCount>();
        public List<string> unlockedSkinIds = new List<string>();
        public List<OwnedCount> ingredients = new List<OwnedCount>();
        public List<OwnedCount> snacks = new List<OwnedCount>();

        public int roomLevel;

        // Room (v2)
        public List<PlacedPiece> pieces = new List<PlacedPiece>();
        public int nextPieceId = 1;

        // The favourite's needs and life (v2)
        public CareState care = new CareState();

        public DateTime LastSavedUtc
        {
            get { return new DateTime(lastSavedUtcTicks, DateTimeKind.Utc); }
        }
    }

    /// <summary>A content id with a count. Lists of these serialise where dictionaries can't.</summary>
    [Serializable]
    public class OwnedCount
    {
        public string id;
        public int count;

        public OwnedCount() { }

        public OwnedCount(string id, int count)
        {
            this.id = id;
            this.count = count;
        }
    }
}
