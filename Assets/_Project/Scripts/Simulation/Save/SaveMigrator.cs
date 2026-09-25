using System;

namespace Squishy.Simulation.Save
{
    /// <summary>Upgrades older saves. v1 and v2 predate the faithful prototype port and start a fresh game.</summary>
    public sealed class SaveMigrator
    {
        public const int CurrentVersion = 3;

        public static SaveMigrator CreateDefault() { return new SaveMigrator(); }

        public void Migrate(SaveData data)
        {
            if (data.version > CurrentVersion)
                throw new SaveVersionException("Save version " + data.version + " is newer than this build (" + CurrentVersion + ").");
            if (data.version < 3) data.state = null; // pre-port layout: begin again from the starter room
            data.version = CurrentVersion;
        }
    }

    public sealed class SaveVersionException : Exception
    {
        public SaveVersionException(string message) : base(message) { }
    }
}
