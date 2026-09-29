using System;

namespace Squishy.Simulation.Save
{
    /// <summary>Upgrades older saves. v1 and v2 predate the faithful prototype port and start a fresh game.</summary>
    public sealed class SaveMigrator
    {
        public const int CurrentVersion = 9;

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
            if (data.version < 6 && data.state != null) data.state.soundOn = true; // v6: sound was off by default (a browser-prototype habit); switch it on once
            if (data.version < 7 && data.state != null) data.state.musicOn = true; // v7: real music replaced the synth loop; switch it on once
            if (data.version < 8 && data.state != null && data.state.combosFound != null) data.state.combosFound.Clear(); // v8: combos need their pieces right next to each other; found under the old loose rule doesn't count
            if (data.version < 9 && data.state != null)
            {
                // v9: the vase, lantern, side table, easel, pouf and books decor were removed (not asked for).
                var gone = new[] { "vase", "lantern", "sidetable", "easel", "pouf", "books" };
                System.Predicate<string> old = k => k != null && Array.IndexOf(gone, k.Split(':')[0]) >= 0;
                data.state.items.RemoveAll(p => old(p.key));
                data.state.storage.RemoveAll(old);
                data.state.owned.RemoveAll(old);
            }
            data.version = CurrentVersion;
        }
    }

    public sealed class SaveVersionException : Exception
    {
        public SaveVersionException(string message) : base(message) { }
    }
}
