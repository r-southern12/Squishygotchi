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
        public string rooms; // Arrange tray tabs it shows under ("kitchen|bathroom")
        public float hop, hopLow; // low pieces the squishy can hop onto: how high it stands on top (low styles)
        public float r;
        /// <summary>Collision circles as flat (x, z, r) triples; empty means one circle of radius r.</summary>
        public float[] circles;
        public int comfort, max, price;
        public string rarity; // fixed rarity for every style of this type (beanbag, trampoline: Epic); empty: from the style
        public bool walk;
    }

    [Serializable] public class FinishData { public string name, tier, color, glow, map; public float rough; public string[] spark; public bool metal; public bool clear; public float opacity; public string inside; public int insideCount; public string[] insideColors; } // clear: see-through jelly; inside: what floats in it (glitter, snow, boba, koi, goldfish, glow)
    [Serializable] public class TierData { public string tier, rarity, color; }
    [Serializable] public class ToolData { public string name, color, rarity, shape; public int price, maxDur; public bool basic; }
    [Serializable] public class IngredientData { public string name, color, rarity, shape; public bool basic; public float size; } // size: how big it shows on the steamer plate (0 = full)
    [Serializable] public class SteamerSkinData { public string name, a, b, t, rarity, glow; public string music, musicName; public float metal, rough; public int stripe; } // music: the track this steamer unlocks (Resources/Music)
    [Serializable] public class ToolSkinData { public string name, col, rarity; }
    [Serializable] public class RecipeData { public string name, bonusNeed, col, note; public int[] ing, tools; public int lvl; public float hunger, cap, bonus; public bool starter; }
    [Serializable] public class SnackData { public string name, color, shape; }
    [Serializable] public class SizeData { public string name; public float s; public int at; } // a squishy's size from copies of it

    /// <summary>
    /// A steamer size (user design, 1 Oct 2026): its radius, how many toys can be out and the most pieces its room levels
    /// reach. Bought with coins (Medium, reachable in the first life), or a prestige reward for full lives (lives: how
    /// many squishies must have lived to old age; Large 1, Extra large 2), granted as soon as the size before it is had.
    /// </summary>
    [Serializable] public class SteamerSizeData { public string name; public float room; public int toys, maxSlots, coins, lives; }
    [Serializable] public class TrayRoomData { public string id, name; }

    /// <summary>A furniture combo: see GameRules.Combos.</summary>
    [Serializable]
    public class ComboData
    {
        public string id, name, lead, act, then, text, @short; // short: a few words for lists (text: the full description)
        public string[] slots;
        public int thenSlot;
        public bool chainLeads, passive;
        public float gap, calmMinutes, cookMul, wearSkip, bonusIng; // gap: how close the pieces must be (edge to edge); 0 = rules.comboGap
        public bool story; // Bedtime story: reads in bed before napping or sleeping for the night
        public string snackAct; // Midnight snack: snacks are carried to the table and eaten with this activity
        public float wiltMul; // Greenhouse: its plants wilt this much as fast (and watering one waters them all)
    }

    [Serializable] public class RoomLevelData { public int slots, need, cost; } // slots: pieces the room holds (every piece counts)

    [Serializable]
    public class ActivityData
    {
        public string role, label, need, also;
        public float dur, rate, perch, front, cap;
        public float alsoMul; // the second need fills at this share of the rate (0: half)
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
        public float comboGap, calmDrain, comboComfort;
        public float nightRestFill; // Rest gained per second while asleep for the night or tucked in // combo pieces must be this close edge to edge; drain multiplier while calm // tips pile up in the coin over its head for this long before it stops adding more
        public float squishPlayGain, squishPlayGainCritical, scrubGain, tuckMinCondition, selfPlayChance;
        public int tipMin, tipMax;
        public float energySeconds; // how long it stays energised (and squishable for coins) after playing by itself
        public int visitCoins, visitHostCoins;
        public float visitCooldownHours = 3; // one visit to each friend in this many hours
        public int neighbourMax, neighbourActiveDays, neighbourOffer; // neighbours: random players (no code) you can add, found among those who played in the last few days
        // Care mistakes: a need left empty this long (seconds, awake) is one mistake, counted once until the need is above
        // careMistakeReset again; each takes careMistakePenalty off the old-age prestige, never below careMistakeFloor of it.
        public float careMistakeSeconds, careMistakePenalty, careMistakeFloor, careMistakeReset;
        public float babyDrain; // needs drain this many times faster in the Baby stage (babies need more looking after)
        public float needVariance; // each squishy's own appetite: per life stage, each need drains up to this much faster or slower
        public float visitFeed, visitPet; // how much a friend's snack and squish top up Hunger and Play
        public float visitPlay, visitPamper, stickerHours; // playing together tops up Play, pampering tops up Clean; a sticker stays this long
        public int visitMaxActs, stickerMax; // caring things a visit pays the host for; stickers in a room at once
        public float dentRadius, flingHold, flingSpeed; // tactile squish: dent size in the world; hold time and speed of the ping // per caring action on a visit: for you, and for the friend you visited
        public int[] stagePrestige; // paid on reaching Young, Adult, Elder, scaled by quality of life so far
        public float lifespanMinDays, lifespanMaxDays, babyDays, prestigeBase, prestigePerQol, trialDays, giftHours;
        public float streakBonusPerDay, streakBonusMax; // a streak of days with missions: each day after the first adds this much to mission coins, up to the max
        public int goalMissions, goalSteamers; // the missions goal: this many missions before it resets pays these steamers
        public int[] goalResetDays; // the goal starts afresh on these days of the week (1 = Monday, 4 = Thursday)
        public bool adminTools;
        public string fullUnlockProductId, adsGameIdAndroid, adsGameIdIos, musicCredit;
        public string[] cosmeticColors; // the shared palette players pick accessory colours from
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
        public TrayRoomData[] trayRooms;
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
                string rar = !string.IsNullOrEmpty(a.rarity) ? a.rarity : hv < 70 ? "Common" : hv < 92 ? "Rare" : "Epic";
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
        public SteamerSizeData[] steamerSizes;

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
