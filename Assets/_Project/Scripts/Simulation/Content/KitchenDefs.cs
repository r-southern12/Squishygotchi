using System;
using System.Collections.Generic;

namespace Squishy.Simulation.Content
{
    [Serializable]
    public class NeedAmount
    {
        public NeedKind need;
        /// <summary>0..1, where 1 is a full need bar.</summary>
        public float amount;
    }

    [Serializable]
    public class IngredientDef
    {
        public string id;
        public string displayName;
        public Rarity rarity;
        /// <summary>Shop price in coins. 0 means steamers only.</summary>
        public int shopPrice;
    }

    [Serializable]
    public class SnackDef
    {
        public string id;
        public string displayName;
        public int shopPrice;
        public float hungerAdd;
        /// <summary>A snack never lifts Hunger above this (0..1).</summary>
        public float hungerCap;
    }

    [Serializable]
    public class RecipeDef
    {
        public string id;
        public string displayName;
        public List<string> ingredientIds = new List<string>();
        public List<string> toolIds = new List<string>();
        /// <summary>The tool shown working on the stove.</summary>
        public string mainToolId;
        public int kitchenLevel = 1;
        public float hungerAdd;
        /// <summary>Hunger can't go above this from this recipe (0..1). Plain congee uses 0.6.</summary>
        public float hungerCap = 1f;
        public List<NeedAmount> bonuses = new List<NeedAmount>();
        /// <summary>Counts toward "cook a recipe" tasks. False for plain congee.</summary>
        public bool countsForTasks = true;
    }

    [Serializable]
    public class ToolDef
    {
        public string id;
        public string displayName;
        public Rarity rarity;
        public int maxUses;
        /// <summary>Shop price in coins. 0 means steamers only.</summary>
        public int shopPrice;
    }

    [Serializable]
    public class ToolSkinDef
    {
        public string id;
        public string displayName;
        public Rarity rarity;
    }

    [Serializable]
    public class KitchenLevelDef
    {
        public int level;
        public int toolsOwnedRequired;
    }

    [Serializable]
    public class MasteryDef
    {
        public int cooksPerStar = 3;
        public int maxStars = 5;
        /// <summary>Added to hunger and bonus per star, as a multiplier (0.1 = +10%).</summary>
        public float bonusPerStar = 0.1f;
    }
}
