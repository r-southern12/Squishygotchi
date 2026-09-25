using System;
using System.Collections.Generic;
using Squishy.Simulation.Content;
using Squishy.Simulation.Core;

namespace Squishy.Simulation.Gacha
{
    /// <summary>Opens since the last result at or above each pity rule's rarity. Lives in the save.</summary>
    [Serializable]
    public class PityState
    {
        /// <summary>One counter per <see cref="DropTableDef.pity"/> rule, same order.</summary>
        public List<int> sinceGuarantee = new List<int>();

        public int OpensSince(int ruleIndex)
        {
            return ruleIndex < sinceGuarantee.Count ? sinceGuarantee[ruleIndex] : 0;
        }

        internal void EnsureSize(int count)
        {
            while (sinceGuarantee.Count < count) sinceGuarantee.Add(0);
        }
    }

    public struct GachaResult
    {
        public Rarity rarity;
        public RewardCategory category;
        /// <summary>True when pity changed the rarity that was rolled.</summary>
        public bool pityTriggered;
        /// <summary>Only meaningful when <see cref="category"/> is Squishy.</summary>
        public bool isFavouriteCopy;
    }

    /// <summary>
    /// Rolls a steamer's rarity and prize category from a drop table. Picking the exact item
    /// (which skin, which ingredient) is left to the content layer, using the same RNG.
    /// Deterministic: the same table, RNG state and pity state always give the same result.
    /// </summary>
    public sealed class GachaRoller
    {
        private readonly DropTableDef _table;
        private readonly IRandom _rng;

        public GachaRoller(DropTableDef table, IRandom rng)
        {
            if (table == null) throw new ArgumentNullException("table");
            if (rng == null) throw new ArgumentNullException("rng");
            var problems = table.Validate();
            if (problems.Count > 0) throw new ArgumentException("Invalid drop table: " + string.Join(" ", problems.ToArray()));
            _table = table;
            _rng = rng;
        }

        public GachaResult Open(PityState pity)
        {
            if (pity == null) throw new ArgumentNullException("pity");
            pity.EnsureSize(_table.pity.Count);

            var result = new GachaResult();
            Rarity rolled = RollRarity();
            result.rarity = ApplyPity(rolled, pity);
            result.pityTriggered = result.rarity != rolled;
            UpdatePity(result.rarity, pity);

            result.category = RollCategory(result.rarity);
            if (result.category == RewardCategory.Squishy)
                result.isFavouriteCopy = _rng.NextDouble() < _table.favouriteCopyChance;
            return result;
        }

        /// <summary>Opens left until a rule's guarantee, counting the next open (the odds button shows this).</summary>
        public int OpensUntilGuarantee(int ruleIndex, PityState pity)
        {
            return _table.pity[ruleIndex].guaranteedWithin - pity.OpensSince(ruleIndex);
        }

        private Rarity RollRarity()
        {
            double pick = _rng.NextDouble() * _table.TotalRarityWeight();
            Rarity last = Rarity.Common;
            for (int i = 0; i < _table.rarities.Count; i++)
            {
                float w = Math.Max(0f, _table.rarities[i].weight);
                if (w <= 0f) continue;
                last = _table.rarities[i].rarity;
                if (pick < w) return last;
                pick -= w;
            }
            return last; // floating point rounding at the very top of the range
        }

        private Rarity ApplyPity(Rarity rolled, PityState pity)
        {
            // Strongest guarantee first: if Epic+ is due, it wins over a Rare+ guarantee.
            Rarity best = rolled;
            for (int i = 0; i < _table.pity.Count; i++)
            {
                var rule = _table.pity[i];
                bool due = pity.sinceGuarantee[i] >= rule.guaranteedWithin - 1;
                if (due && best < rule.minRarity) best = rule.minRarity;
            }
            return best;
        }

        private void UpdatePity(Rarity got, PityState pity)
        {
            for (int i = 0; i < _table.pity.Count; i++)
            {
                if (got >= _table.pity[i].minRarity) pity.sinceGuarantee[i] = 0;
                else pity.sinceGuarantee[i]++;
            }
        }

        private RewardCategory RollCategory(Rarity rarity)
        {
            var table = _table.FindCategoryTable(rarity);
            float total = 0f;
            for (int i = 0; i < table.categories.Count; i++) total += Math.Max(0f, table.categories[i].weight);

            double pick = _rng.NextDouble() * total;
            RewardCategory last = table.categories[0].category;
            for (int i = 0; i < table.categories.Count; i++)
            {
                float w = Math.Max(0f, table.categories[i].weight);
                if (w <= 0f) continue;
                last = table.categories[i].category;
                if (pick < w) return last;
                pick -= w;
            }
            return last;
        }
    }
}
