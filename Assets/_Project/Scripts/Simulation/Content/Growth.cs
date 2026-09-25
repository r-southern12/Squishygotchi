using System.Collections.Generic;

namespace Squishy.Simulation.Content
{
    public static class Growth
    {
        /// <summary>The largest size tier whose copy requirement is met. Null if the table is empty.</summary>
        public static SizeTierDef SizeFor(List<SizeTierDef> tiers, int copies)
        {
            SizeTierDef best = null;
            for (int i = 0; i < tiers.Count; i++)
            {
                var t = tiers[i];
                if (t.copiesNeeded <= copies && (best == null || t.copiesNeeded > best.copiesNeeded)) best = t;
            }
            if (best == null && tiers.Count > 0)
            {
                best = tiers[0];
                for (int i = 1; i < tiers.Count; i++) if (tiers[i].copiesNeeded < best.copiesNeeded) best = tiers[i];
            }
            return best;
        }

        public static RoomLevelDef RoomLevel(List<RoomLevelDef> levels, int level)
        {
            for (int i = 0; i < levels.Count; i++) if (levels[i].level == level) return levels[i];
            return null;
        }

        public static int CopiesOf(List<Squishy.Simulation.Save.OwnedCount> owned, string id)
        {
            for (int i = 0; i < owned.Count; i++) if (owned[i].id == id) return owned[i].count;
            return 0;
        }
    }
}
