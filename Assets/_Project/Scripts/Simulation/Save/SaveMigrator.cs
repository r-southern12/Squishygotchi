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
        public const int CurrentVersion = 1;

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
            return new SaveMigrator(new ISaveMigration[0]);
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
