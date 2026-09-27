using System.Collections.Generic;

namespace Squishy.Simulation.Game
{
    /// <summary>The published odds, measured from the real rules (so the odds screen can never drift from the game).</summary>
    public sealed class OddsReport
    {
        public float common, rare, epic, legendary;   // per steamer layer, pity included
        public readonly List<KeyValuePair<string, float>> kinds = new List<KeyValuePair<string, float>>();
        public float favouriteCopy;                   // share of squishy prizes that are a copy of your favourite
    }

    public sealed partial class GameRules
    {
        private static OddsReport _odds;

        /// <summary>Rolls many steamers on a scratch game (your own save is untouched) and reports what comes out.</summary>
        public static OddsReport MeasureOdds(GameContent c, int pulls = 40000)
        {
            if (_odds != null) return _odds;
            var g = new GameRules(c, NewState(c, 20260928));
            int[] tier = new int[4];
            var kind = new Dictionary<string, int>();
            var order = new List<string>();
            int sq = 0, fav = 0;
            for (int n = 0; n < pulls; n++)
            {
                var rw = g.RollReward();
                tier[GameContent.RarityRank(rw.rar)]++;
                string k = rw.type == "item" ? "Furniture" : rw.type == "kit" || rw.type == "tool" ? "Kitchen kits" : rw.type == "sq" ? "Squishies"
                    : rw.type == "tskin" ? "Tool skins" : rw.type == "food" ? "Rare ingredients" : "Steamer skins";
                if (!kind.ContainsKey(k)) { kind[k] = 0; order.Add(k); }
                kind[k]++;
                if (rw.type == "sq") { sq++; if (rw.i == g.S.favIdx) fav++; }
            }
            var r = new OddsReport { common = tier[0] / (float)pulls, rare = tier[1] / (float)pulls, epic = tier[2] / (float)pulls, legendary = tier[3] / (float)pulls, favouriteCopy = sq > 0 ? fav / (float)sq : 0 };
            order.Sort((a, b) => kind[b].CompareTo(kind[a]));
            foreach (var k in order) r.kinds.Add(new KeyValuePair<string, float>(k, kind[k] / (float)pulls));
            return _odds = r;
        }
    }
}
