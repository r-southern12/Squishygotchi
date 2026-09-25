using System;
using System.Collections.Generic;
using Squishy.Simulation.Content;

namespace Squishy.Simulation.Gacha
{
    [Serializable]
    public class RarityWeight
    {
        public Rarity rarity;
        /// <summary>Relative weight. Weights don't need to sum to 1; percentages are derived.</summary>
        public float weight;
    }

    /// <summary>"<see cref="minRarity"/> or better guaranteed within <see cref="guaranteedWithin"/> opens."</summary>
    [Serializable]
    public class PityRule
    {
        public Rarity minRarity;
        public int guaranteedWithin;
    }

    [Serializable]
    public class CategoryWeight
    {
        public RewardCategory category;
        public float weight;
    }

    /// <summary>What a prize of a given rarity turns out to be.</summary>
    [Serializable]
    public class CategoryTable
    {
        public Rarity rarity;
        public List<CategoryWeight> categories = new List<CategoryWeight>();
    }

    [Serializable]
    public class DropTableDef
    {
        public List<RarityWeight> rarities = new List<RarityWeight>();
        public List<PityRule> pity = new List<PityRule>();
        public List<CategoryTable> categoryTables = new List<CategoryTable>();
        /// <summary>Chance a squishy result is a copy of the current favourite (grows it).</summary>
        public float favouriteCopyChance;

        public float TotalRarityWeight()
        {
            float total = 0f;
            for (int i = 0; i < rarities.Count; i++) total += Math.Max(0f, rarities[i].weight);
            return total;
        }

        /// <summary>Published chance of a rarity (0..1), before pity. Used by the odds screen.</summary>
        public float PublishedChance(Rarity rarity)
        {
            float total = TotalRarityWeight();
            if (total <= 0f) return 0f;
            float w = 0f;
            for (int i = 0; i < rarities.Count; i++)
                if (rarities[i].rarity == rarity) w += Math.Max(0f, rarities[i].weight);
            return w / total;
        }

        public CategoryTable FindCategoryTable(Rarity rarity)
        {
            for (int i = 0; i < categoryTables.Count; i++)
                if (categoryTables[i].rarity == rarity) return categoryTables[i];
            return null;
        }

        /// <summary>Problems that would make the table unusable. Empty when valid.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (TotalRarityWeight() <= 0f) problems.Add("Rarity weights must add up to more than zero.");
            for (int i = 0; i < pity.Count; i++)
                if (pity[i].guaranteedWithin < 1) problems.Add("Pity rule for " + pity[i].minRarity + " needs guaranteedWithin of at least 1.");
            for (int i = 0; i < rarities.Count; i++)
            {
                if (rarities[i].weight <= 0f) continue;
                var table = FindCategoryTable(rarities[i].rarity);
                if (table == null || table.categories.Count == 0)
                    problems.Add("No category table for " + rarities[i].rarity + ".");
            }
            if (favouriteCopyChance < 0f || favouriteCopyChance > 1f) problems.Add("favouriteCopyChance must be between 0 and 1.");
            return problems;
        }
    }
}
