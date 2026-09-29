using System;
using System.Collections.Generic;

namespace Squishy.Simulation.Game
{
    // Plain content records, loaded from Resources/Content/game_content.json (JsonUtility-friendly: public fields).
    // Adding a style, recipe, finish or task is a data change only.

    [Serializable] public class StyleData { public string id, name, shortName, group, pat, plant, plantName; public string[] pal, shelf; public bool low, round; public int[] eventMonths; }

    [Serializable]
    public class ItemTypeData
    {
        public string id, name, cat, role, face;
        /// <summary>Items sharing a slot are swapped for one another (one toy out at a time).</summary>
        public string slot;
        public float r;
        /// <summary>Collision circles as flat (x, z, r) triples; empty means one circle of radius r.</summary>
        public float[] circles;
        public int comfort, size, max, price;
        public bool walk;
    }

    [Serializable] public class FinishData { public string name, tier, color, glow, map; public float rough; public string[] spark; public bool metal; public bool clear; public float opacity; public string inside; public int insideCount; public string[] insideColors; } // clear: see-through jelly; inside: what floats in it (glitter, snow, boba, koi, goldfish, glow)
    [Serializable] public class TierData { public string tier, rarity, color; }
    [Serializable] public class ToolData { public string name, color, rarity, shape; public int price, maxDur; public bool basic; }
    [Serializable] public class IngredientData { public string name, color, rarity, shape; public bool basic; }
    [Serializable] public class SteamerSkinData { public string name, a, b, t, rarity, glow; public string music, musicName; public float metal, rough; } // music: the track this steamer unlocks (Resources/Music)
    [Serializable] public class ToolSkinData { public string name, col, rarity; }
    [Serializable] public class RecipeData { public string name, bonusNeed, col, note; public int[] ing, tools; public int lvl; public float hunger, cap, bonus; public bool starter; }
    [Serializable] public class SnackData { public string name, color, shape; }
    [Serializable] public class SizeData { public string name; public float s; public int at, decor; }
    /// <summary>A furniture combo: see GameRules.Combos.</summary>
    [Serializable]
    public class ComboData
    {
        public string id, name, lead, act, then, text;
        public string[] slots;
        public int thenSlot;
        public bool chainLeads, passive;
        public float gap, calmMinutes, cookMul, wearSkip, bonusIng; // gap: how close the pieces must be (edge to edge); 0 = rules.comboGap
    }

    [Serializable] public class RoomLevelData { public float r; public int slots, need, cost; }

    [Serializable]
    public class ActivityData
    {
        public string role, label, need, also;
        public float dur, rate, perch, front, cap;
        public bool toward, needSeat, sleep, scrub, inside, closed; // closed: eyes shut (a quiet moment)
    }

    [Serializable] public class TaskData { public string id, text, ev; public int goal, coins; public bool time; public string[] requires; } // ev: the event it counts (defaults to id); requires: any of these item types placed, or "friend"
    [Serializable] public class PlacedData { public string key; public float x, z, ry; }
    [Serializable] public class CountData { public int i, n; }

    [Serializable]
    public class RulesData
    {
        public float decayHunger, decayPlay, decayRest, decayClean;
        public float comfortSlowPerPoint, comfortSlowMax, setBonus, plantWiltRate;
        public int setCount;
        public float deathSeconds, dayLength, selfCareCap, happyBase, happyComfortDivisor;
        public float pLegendary, pEpic, pRare, favouriteChance, pTwoLayers, pThreeLayers;
        public int pityRare, pityEpic, pityLegendary, taskMemory, nightStartHour, nightEndHour, nightWakeByHour, nightAskSeconds;
        public float nightDrain;
        public int[] nightSnoozeMinutes; // the bedtime prompt's snooze choices
        public int dupeItemCoins, dupeToolSkinCoins, dupeSteamerSkinCoins, dupeToolCoins, foodPerDrop, kitIngredients;
        public int steamerPrice, snackPrice, ingredientPrice, newToolPrice, minRepair, taskSteamers;
        public float taskCooldownHours;
        public int cooksPerLevel; // cooks of one recipe to raise it a level (a star)
        public float tipStackSeconds;
        public float comboGap, calmDrain; // combo pieces must be this close edge to edge; drain multiplier while calm // tips pile up in the coin over its head for this long before it stops adding more
        public float squishPlayGain, squishPlayGainCritical, scrubGain, tuckMinCondition, selfPlayChance;
        public int tipMin, tipMax;
        public float energySeconds; // how long it stays energised (and squishable for coins) after playing by itself
        public int visitCoins, visitHostCoins;
        public float visitFeed, visitPet; // how much a friend's snack and squish top up Hunger and Play
        public float dentRadius, flingHold, flingSpeed; // tactile squish: dent size in the world; hold time and speed of the ping // per caring action on a visit: for you, and for the friend you visited
        public int[] stagePrestige; // paid on reaching Young, Adult, Elder, scaled by quality of life so far
        public float lifespanMinDays, lifespanMaxDays, babyDays, prestigeBase, prestigePerQol, trialDays, giftHours;
        public int weeklyGoal, weeklySteamers;
        public bool adminTools;
        public string fullUnlockProductId, adsGameIdAndroid, adsGameIdIos, musicCredit;
    }

    [Serializable]
    public class StarterData
    {
        public int coins, steamers, favourite, roomLevel, age;
        public float[] needs;
        public string[] owned;
        public CountData[] squishies;
        public int[] pantry, snacks;
        public float roomScale;
        public PlacedData[] room;
        public string[] storage;
    }

    /// <summary>One catalogue entry: an item type in a style ("bed:cottage").</summary>
    public sealed class CatalogueItem
    {
        public string key, arch, style, name, rarity, cat;
    }

    [Serializable]
    public class GameContent
    {
        public StyleData[] styles;
        public ItemTypeData[] types;
        public ItemTypeData tomb;
        public FinishData[] finishes;
        public TierData[] tiers;
        public ToolData[] tools;
        public IngredientData[] pantry;
        public SteamerSkinData[] skins;
        public ToolSkinData[] toolSkins;
        public RecipeData[] recipes;
        public SnackData[] snacks;
        public SizeData[] sizes;
        public RoomLevelData[] roomLevels;
        public ActivityData[] activities;
        public TaskData[] tasks;
        public RulesData rules;
        public StarterData starter;
        public CosmeticData[] cosmetics;
        public ComboData[] combos;
        public TierRewardData[] tierRewards;

        [NonSerialized] public List<CatalogueItem> Catalogue;
        [NonSerialized] private Dictionary<string, CatalogueItem> _catByKey;
        [NonSerialized] private Dictionary<string, StyleData> _styleById;
        [NonSerialized] private Dictionary<string, ItemTypeData> _typeById;
        [NonSerialized] private Dictionary<string, ActivityData> _actByRole;

        /// <summary>Builds lookups and the style x type catalogue. Call once after loading.</summary>
        public void Init()
        {
            _styleById = new Dictionary<string, StyleData>();
            foreach (var s in styles) _styleById[s.id] = s;
            _typeById = new Dictionary<string, ItemTypeData>();
            foreach (var t in types) _typeById[t.id] = t;
            _typeById[tomb.id] = tomb;
            _actByRole = new Dictionary<string, ActivityData>();
            foreach (var a in activities) _actByRole[a.role] = a;
            Catalogue = new List<CatalogueItem>();
            _catByKey = new Dictionary<string, CatalogueItem>();
            foreach (var s in styles)
            foreach (var a in types)
            {
                string key = a.id + ":" + s.id;
                uint hv = Hash(key) % 100;
                string rar = a.size > 0 ? "Epic" : hv < 70 ? "Common" : hv < 92 ? "Rare" : "Epic";
                var it = new CatalogueItem { key = key, arch = a.id, style = s.id, rarity = rar, cat = a.cat, name = s.shortName + " " + (a.id == "plant" && !string.IsNullOrEmpty(s.plantName) ? s.plantName : a.name) };
                Catalogue.Add(it);
                _catByKey[key] = it;
            }
        }

        public StyleData Style(string id) { StyleData s; return id != null && _styleById.TryGetValue(id, out s) ? s : null; }
        public ItemTypeData Type(string id) { ItemTypeData t; return id != null && _typeById.TryGetValue(id, out t) ? t : null; }
        public ActivityData Activity(string role) { ActivityData a; return role != null && _actByRole.TryGetValue(role, out a) ? a : null; }
        public CatalogueItem Cat(string key) { CatalogueItem c; return key != null && _catByKey.TryGetValue(key, out c) ? c : null; }

        public string FinishRarity(FinishData f) { foreach (var t in tiers) if (t.tier == f.tier) return t.rarity; return "Common"; }
        public string TierColor(string tier) { foreach (var t in tiers) if (t.tier == tier) return t.color; return "#D8C7AE"; }

        /// <summary>Squishy size index for a number of copies.</summary>
        public int SizeIdxFor(int copies)
        {
            int i = 0;
            for (int k = 0; k < sizes.Length; k++) if (copies >= sizes[k].at) i = k;
            return i;
        }

        /// <summary>Stations are limited per room; decor is limited by room level slots.</summary>
        public bool IsDecor(string arch)
        {
            if (arch == tomb.id) return false;
            var t = Type(arch);
            return t != null && t.max == 0;
        }

        /// <summary>Item types sharing this type's slot (itself when it has none).</summary>
        public bool SameSlot(string a, string b)
        {
            if (a == b) return true;
            var ta = Type(a); var tb = Type(b);
            return ta != null && tb != null && !string.IsNullOrEmpty(ta.slot) && ta.slot == tb.slot;
        }

        public int MaxPerRoom(string arch) { var t = Type(arch); return t != null && t.max > 0 ? t.max : 1; }

        /// <summary>FNV-1a, same as the prototype, so every item keeps its rarity.</summary>
        public static uint Hash(string s)
        {
            uint h = 2166136261;
            foreach (char ch in s) { h ^= ch; h *= 16777619; }
            return h;
        }

        public static int RarityRank(string r)
        {
            switch (r) { case "Rare": return 1; case "Epic": return 2; case "Legendary": return 3; default: return 0; }
        }
    }
}
