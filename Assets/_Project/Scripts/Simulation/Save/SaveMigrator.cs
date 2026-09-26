using System;

namespace Squishy.Simulation.Save
{
    /// <summary>Upgrades older saves. v1 and v2 predate the faithful prototype port and start a fresh game.</summary>
    public sealed class SaveMigrator
    {
        public const int CurrentVersion = 5;

        public static SaveMigrator CreateDefault() { return new SaveMigrator(); }

        public void Migrate(SaveData data)
        {
            if (data.version > CurrentVersion)
                throw new SaveVersionException("Save version " + data.version + " is newer than this build (" + CurrentVersion + ").");
            if (data.version < 3) data.state = null; // pre-port layout: begin again from the starter room
            else if (data.version < 4 && data.state != null)
            {
                // v4 adds swappable toys: hand existing players one of each to try.
                foreach (var k in new[] { "pomwand:candy", "bubbles:aegean", "xylophone:folk", "slide:cottage" })
                {
                    if (!data.state.owned.Contains(k)) data.state.owned.Add(k);
                    if (!data.state.storage.Contains(k) && !data.state.items.Exists(p => p.key == k)) data.state.storage.Add(k);
                }
            }
            if (data.version < 5 && data.state != null)
            {
                // v5 replaces the recipe list with dumpling-filling dishes: old mastery no longer matches any recipe.
                data.state.recipeXP = null;
            }
            data.version = CurrentVersion;
        }
    }

    public sealed class SaveVersionException : Exception
    {
        public SaveVersionException(string message) : base(message) { }
    }
}
