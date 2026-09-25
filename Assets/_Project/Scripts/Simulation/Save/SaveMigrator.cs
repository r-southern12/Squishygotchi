using System;
using System.Collections.Generic;

namespace Squishy.Simulation.Save
{
    /// <summary>Upgrades a save from <see cref="FromVersion"/> to FromVersion + 1.</summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }
        void Apply(SaveData data);
    }

    public sealed class SaveMigrator
    {
        /// <summary>The version new saves are written with.</summary>
        public const int CurrentVersion = 2;

        private readonly Dictionary<int, ISaveMigration> _byFromVersion = new Dictionary<int, ISaveMigration>();

        public SaveMigrator(IEnumerable<ISaveMigration> migrations)
        {
            if (migrations == null) return;
            foreach (var m in migrations)
            {
                if (_byFromVersion.ContainsKey(m.FromVersion))
                    throw new ArgumentException("Two migrations from version " + m.FromVersion + ".");
                _byFromVersion.Add(m.FromVersion, m);
            }
        }

        /// <summary>The default chain used by the game. Add each new migration here.</summary>
        public static SaveMigrator CreateDefault()
        {
            return new SaveMigrator(new ISaveMigration[] { new V1AddCareAndRoom() });
        }

        /// <summary>v1 saves had no needs or room. Start them healthy; the starter room is added on load.</summary>
        private sealed class V1AddCareAndRoom : ISaveMigration
        {
            public int FromVersion { get { return 1; } }

            public void Apply(SaveData data)
            {
                if (data.care == null) data.care = new Care.CareState();
                if (data.pieces == null) data.pieces = new List<Room.PlacedPiece>();
                if (data.nextPieceId < 1) data.nextPieceId = 1;
            }
        }

        /// <summary>Runs migrations in order until the save reaches <paramref name="targetVersion"/>.</summary>
        public void Migrate(SaveData data, int targetVersion)
        {
            if (data.version > targetVersion)
                throw new SaveVersionException("Save is version " + data.version + " but this build only understands up to " + targetVersion + ".");

            while (data.version < targetVersion)
            {
                ISaveMigration step;
                if (!_byFromVersion.TryGetValue(data.version, out step))
                    throw new SaveVersionException("No migration from save version " + data.version + ".");
                step.Apply(data);
                data.version++;
            }
        }

        public void Migrate(SaveData data)
        {
            Migrate(data, CurrentVersion);
        }
    }

    public sealed class SaveVersionException : Exception
    {
        public SaveVersionException(string message) : base(message) { }
    }
}
