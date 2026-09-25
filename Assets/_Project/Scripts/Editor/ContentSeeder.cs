using System;
using System.Collections.Generic;
using System.IO;
using Squishy.Data;
using Squishy.Simulation.Content;
using Squishy.Simulation.Gacha;
using UnityEditor;
using UnityEngine;

namespace Squishy.EditorTools
{
    /// <summary>
    /// One-off import of the tables in docs/spec.md into data assets, so there's real content to
    /// build against. It only creates assets that don't exist yet and never overwrites tuning:
    /// once an asset exists, the asset is the source of truth, not this file.
    /// </summary>
    public static class ContentSeeder
    {
        public const string ContentRoot = "Assets/_Project/Content";
        public const string DatabasePath = ContentRoot + "/ContentDatabase.asset";

        [MenuItem("Squishy/Content/Seed From Spec (creates missing assets only)")]
        public static void SeedFromSpec()
        {
            int before = CountAssets();
            SeedTables();
            SeedStyles();
            SeedItemTypes();
            SeedFinishes();
            SeedKitchen();
            SeedTasks();
            AssetDatabase.SaveAssets();
            RebuildDatabase();
            Debug.Log("Seeded content from spec: " + (CountAssets() - before) + " new assets in " + ContentRoot + ".");
        }

        [MenuItem("Squishy/Content/Rebuild Database")]
        public static void RebuildDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DatabasePath);
            if (db == null)
            {
                EnsureFolder(ContentRoot);
                db = ScriptableObject.CreateInstance<ContentDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            db.economy = FindFirst<EconomyAsset>();
            db.dropTable = FindFirst<DropTableAsset>();
            db.progression = FindFirst<ProgressionAsset>();
            db.styles = FindAll<StyleAsset>();
            db.itemTypes = FindAll<ItemTypeAsset>();
            db.skins = FindAll<SkinAsset>();
            db.finishes = FindAll<SquishyFinishAsset>();
            db.ingredients = FindAll<IngredientAsset>();
            db.snacks = FindAll<SnackAsset>();
            db.recipes = FindAll<RecipeAsset>();
            db.tools = FindAll<ToolAsset>();
            db.toolSkins = FindAll<ToolSkinAsset>();
            db.tasks = FindAll<TaskAsset>();

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        // ---- Tables -------------------------------------------------------------------

        private static void SeedTables()
        {
            Make<EconomyAsset>("Tables", "Economy", a => { a.def = new EconomyDef(); });

            Make<DropTableAsset>("Tables", "DropTable", a =>
            {
                var t = a.def = new DropTableDef();
                t.rarities.Add(Weight(Rarity.Common, 76));
                t.rarities.Add(Weight(Rarity.Rare, 18));
                t.rarities.Add(Weight(Rarity.Epic, 5));
                t.rarities.Add(Weight(Rarity.Legendary, 1));
                t.pity.Add(new PityRule { minRarity = Rarity.Rare, guaranteedWithin = 10 });
                t.pity.Add(new PityRule { minRarity = Rarity.Epic, guaranteedWithin = 50 });
                t.favouriteCopyChance = 0.4f;
                // Category splits per rarity, from the prototype's rollReward().
                t.categoryTables.Add(Categories(Rarity.Common,
                    RewardCategory.FurnitureSkin, 52, RewardCategory.Ingredient, 28, RewardCategory.Squishy, 10,
                    RewardCategory.Tool, 6, RewardCategory.ToolSkin, 4));
                t.categoryTables.Add(Categories(Rarity.Rare,
                    RewardCategory.FurnitureSkin, 50, RewardCategory.Ingredient, 14, RewardCategory.Squishy, 16,
                    RewardCategory.Tool, 10, RewardCategory.ToolSkin, 7, RewardCategory.SteamerSkin, 3));
                t.categoryTables.Add(Categories(Rarity.Epic,
                    RewardCategory.FurnitureSkin, 62, RewardCategory.Squishy, 28, RewardCategory.ToolSkin, 6,
                    RewardCategory.SteamerSkin, 4));
                t.categoryTables.Add(Categories(Rarity.Legendary,
                    RewardCategory.Squishy, 50, RewardCategory.SteamerSkin, 50));
            });

            Make<ProgressionAsset>("Tables", "Progression", a =>
            {
                a.sizeTiers = new List<SizeTierDef>
                {
                    new SizeTierDef { size = SquishySize.Mini, copiesNeeded = 1, relativeScale = 1f },
                    new SizeTierDef { size = SquishySize.Standard, copiesNeeded = 3, relativeScale = 1.5f },
                    new SizeTierDef { size = SquishySize.Jumbo, copiesNeeded = 6, relativeScale = 2.1f },
                    new SizeTierDef { size = SquishySize.Giant, copiesNeeded = 12, relativeScale = 2.9f },
                    new SizeTierDef { size = SquishySize.SuperMega, copiesNeeded = 25, relativeScale = 4f },
                };
                a.roomLevels = new List<RoomLevelDef>
                {
                    new RoomLevelDef { level = 1, relativeWidth = 0.66f, decorSlots = 4, availableAtLaunch = false },
                    new RoomLevelDef { level = 2, relativeWidth = 0.77f, decorSlots = 6 },
                    new RoomLevelDef { level = 3, relativeWidth = 0.89f, decorSlots = 9, squishiesRequired = 5, coinCost = 400 },
                    new RoomLevelDef { level = 4, relativeWidth = 1f, decorSlots = 12, squishiesRequired = 10, coinCost = 1000 },
                };
                a.kitchenLevels = new List<KitchenLevelDef>
                {
                    new KitchenLevelDef { level = 1, toolsOwnedRequired = 0 },
                    new KitchenLevelDef { level = 2, toolsOwnedRequired = 2 },
                    new KitchenLevelDef { level = 3, toolsOwnedRequired = 4 },
                    new KitchenLevelDef { level = 4, toolsOwnedRequired = 6 },
                };
                a.mastery = new MasteryDef();
            });
        }

        // ---- Styles -------------------------------------------------------------------

        private static void SeedStyles()
        {
            Style("teahouse", "Cantonese Teahouse", "Teahouse", StyleGroup.World, "#7A3B2A", "#B8332E", "#6FA58E", "#D8A94A", "#F2E3C6");
            Style("tatami", "Japanese Tatami", "Tatami", StyleGroup.World, "#D9BF8F", "#2F4A6E", "#B7C48A", "#3A302A", "#F4EEDF");
            Style("hanok", "Korean Hanok", "Hanok", StyleGroup.World, "#C79A62", "#8FB8A8", "#F1EADB", "#5B4636", "#EFE6D2");
            Style("blockprint", "Indian Block-print", "Block-print", StyleGroup.World, "#8E5A35", "#2E4E8C", "#E8A93B", "#B5452F", "#F5E9D3");
            Style("riad", "Moroccan Riad", "Riad", StyleGroup.World, "#8A5B3A", "#1F7F86", "#2D4E9E", "#C9A04A", "#F2E4CF");
            Style("persian", "Persian Garden", "Persian", StyleGroup.World, "#6E3B2A", "#9E2B35", "#1F4F7A", "#D4A64A", "#F1E4CC");
            Style("talavera", "Mexican Talavera", "Talavera", StyleGroup.World, "#B9643E", "#2C4FA0", "#F2C94C", "#3A6B3F", "#F6F0E3");
            Style("textile", "West African Textile", "Textile", StyleGroup.World, "#5A3A26", "#D19A2E", "#2F3E6B", "#9A3D24", "#EADCC2");
            Style("aegean", "Mediterranean", "Aegean", StyleGroup.World, "#C9A67A", "#2F6FB0", "#F4F1EA", "#7F8F4E", "#FAF7F0");
            Style("nordic", "Scandinavian", "Nordic", StyleGroup.World, "#D9C3A0", "#D98E8E", "#D8D8D2", "#4A4A48", "#F7F5F0");
            Style("andean", "Andean Weave", "Andean", StyleGroup.World, "#8B5E3C", "#E0457B", "#F2A03D", "#2E8B6A", "#F3E7D3");
            Style("folk", "Eastern European Folk", "Folk", StyleGroup.World, "#B07A48", "#C73A36", "#F3E6C9", "#2F5E3A", "#FBF3E3");
            Style("cottage", "Cottagecore", "Cottage", StyleGroup.Mood, "#B48A5E", "#E3A0A8", "#A9C39A", "#7A6048", "#FBF3E6");
            Style("midcentury", "Mid-century", "Mid-century", StyleGroup.Mood, "#A0642F", "#E08A2E", "#3F8A8C", "#2B2B2B", "#F0E6D2");
            Style("minimal", "Minimal", "Minimal", StyleGroup.Mood, "#E6E1D8", "#BDB6AA", "#F4F2EE", "#3C3A37", "#FFFFFF");
            Style("candy", "Candy Shop", "Candy", StyleGroup.Mood, "#F4B8C8", "#8ED1C6", "#FFF1A8", "#E86A92", "#FFF7FA");
            Style("seaside", "Seaside", "Seaside", StyleGroup.Mood, "#D8C29A", "#3C8DBC", "#9FD3E0", "#E8765A", "#F4FAFA");
            Style("space", "Star Station", "Star", StyleGroup.Mood, "#3A3F5C", "#7B6CF6", "#2A2D45", "#C9D1E8", "#CBD2E6");
            Style("spooky", "Spooky Manor", "Spooky", StyleGroup.Mood, "#3B2E3A", "#E07B39", "#5E4A74", "#1E1A1F", "#D9CFC0");
            Style("pixel", "Pixel Den", "Pixel", StyleGroup.Mood, "#2E3440", "#4DD9A5", "#E0457B", "#6C7AE0", "#D8DEE9");
            Style("greenhouse", "Greenhouse", "Greenhouse", StyleGroup.Mood, "#9C7A54", "#5E9E5A", "#D9E8C4", "#6B5B45", "#F2F4E8");
            Style("bakery", "Bakery", "Bakery", StyleGroup.Mood, "#C89A64", "#E8C27A", "#F6E6CF", "#8C5A3A", "#FFF8EC");
            Style("diner", "Retro Diner", "Diner", StyleGroup.Mood, "#E4E4E0", "#D93A3A", "#69C3C9", "#2B2B2B", "#FFFFFF");
            Style("library", "Cosy Library", "Library", StyleGroup.Mood, "#5E3B24", "#8C2F39", "#3D5A40", "#C9A44A", "#EFE2C8");
        }

        private static void Style(string id, string name, string shortName, StyleGroup group, params string[] palette)
        {
            Make<StyleAsset>("Styles", id, a =>
            {
                a.def = new StyleDef { id = id, displayName = name, shortName = shortName, group = group };
                a.palette = new Color[palette.Length];
                for (int i = 0; i < palette.Length; i++) a.palette[i] = Hex(palette[i]);
            });
        }

        // ---- Item types ---------------------------------------------------------------

        private static void SeedItemTypes()
        {
            Item("bed", "Bed", ItemKind.Station, NeedMask.Rest, 2, 1);
            Item("stool", "Stool", ItemKind.Station, NeedMask.None, 1, 2);
            Item("tea_table", "Tea table", ItemKind.Station, NeedMask.Rest | NeedMask.Hunger, 1, 1);
            Item("stove", "Stove", ItemKind.Station, NeedMask.Hunger, 0, 1);
            Item("pantry", "Pantry cupboard", ItemKind.Station, NeedMask.Hunger, 0, 1);
            Item("sink", "Sink", ItemKind.Station, NeedMask.Clean, 0, 1);
            Item("bathtub", "Bathtub", ItemKind.Station, NeedMask.Clean, 1, 1);
            Item("shower", "Shower", ItemKind.Station, NeedMask.Clean, 0, 1);
            Item("ball", "Ball", ItemKind.Toy, NeedMask.Play, 0, 1);
            Item("trampoline", "Trampoline", ItemKind.Toy, NeedMask.Play, 0, 1, SquishySize.Jumbo);
            Item("beanbag", "Beanbag", ItemKind.Decor, NeedMask.Rest, 2, 0, SquishySize.Giant);
            Item("floor_cushion", "Floor cushion", ItemKind.Decor, NeedMask.Rest, 1, 0);
            Item("lamp", "Lamp", ItemKind.Decor, NeedMask.None, 2, 0);
            Item("plant", "Plant", ItemKind.Decor, NeedMask.None, 2, 0);
            Item("shelf", "Shelf", ItemKind.Decor, NeedMask.None, 2, 0);
            Item("wardrobe", "Wardrobe", ItemKind.Decor, NeedMask.None, 2, 0);
            Item("rug", "Rug", ItemKind.Decor, NeedMask.None, 1, 0);
            Item("wall_panel", "Wall panel", ItemKind.Decor, NeedMask.None, 0, 0);
            Item("folding_screen", "Folding screen", ItemKind.Decor, NeedMask.None, 1, 0);

            Make<ItemTypeAsset>("ItemTypes", "tombstone", a =>
            {
                a.def = new ItemTypeDef { id = "tombstone", displayName = "Tombstone", kind = ItemKind.Keepsake, storable = false, sellable = false };
            });
        }

        private static void Item(string id, string name, ItemKind kind, NeedMask needs, int comfort, int maxPerRoom,
            SquishySize minSize = SquishySize.Mini)
        {
            Make<ItemTypeAsset>("ItemTypes", id, a =>
            {
                a.def = new ItemTypeDef
                {
                    id = id, displayName = name, kind = kind, needs = needs, comfort = comfort,
                    maxPerRoom = maxPerRoom, minSquishySize = minSize,
                };
            });
        }

        // ---- Squishy finishes ---------------------------------------------------------

        private static void SeedFinishes()
        {
            var c = Rarity.Common;
            var r = Rarity.Rare;
            var e = Rarity.Epic;
            string[][] common =
            {
                new[] { "peach", "Peach", "#F5C3A0" }, new[] { "butter", "Butter", "#F6DE8D" },
                new[] { "taro", "Taro", "#C9B3E0" }, new[] { "matcha", "Matcha", "#B5CF8E" },
                new[] { "sky", "Sky", "#A9D2EE" }, new[] { "rose", "Rose", "#F2B1B8" },
                new[] { "cocoa", "Cocoa", "#B98A6B" }, new[] { "snow", "Snow", "#F4F1EA" },
            };
            foreach (var f in common) Finish(f[0], f[1], "Common", c, f[2], 0.15f, 2.5f, null);

            Finish("pinky", "Pinky", "Glitter", r, "#F3A6BD", 0.62f, 2f, null, "#FFFFFF", "#FFE3EC", "#FFE9B0");
            Finish("gold_dust", "Gold Dust", "Glitter", r, "#E8C46A", 0.65f, 2f, null, "#FFFFFF", "#FFF2C2");
            Finish("frost", "Frost", "Glitter", r, "#D7DCE3", 0.7f, 2f, null, "#FFFFFF", "#DDEBFF");
            Finish("lilac_fizz", "Lilac Fizz", "Glitter", r, "#CDB2F0", 0.65f, 2f, null, "#FFFFFF", "#FFE3FA");
            Finish("koi", "Koi", "Pattern", r, "#FFFFFF", 0.5f, 2.5f, null);
            Finish("strawberry", "Strawberry", "Pattern", r, "#FFFFFF", 0.5f, 2.5f, null);
            Finish("sesame", "Sesame", "Pattern", r, "#FFFFFF", 0.4f, 2.5f, null);

            Finish("nebula", "Nebula", "Galaxy", e, "#40379A", 0.7f, 2f, null, "#8BF3FF", "#FF8ADB", "#FFD66B", "#FFFFFF");
            Finish("aurora", "Aurora", "Galaxy", e, "#1F5E6E", 0.7f, 2f, null, "#9BFFB8", "#FF9BE6", "#FFFFFF");
            Finish("supernova", "Supernova", "Galaxy", e, "#6E1F4F", 0.7f, 2f, null, "#FFD66B", "#FF7A59", "#FFFFFF");
            Finish("glowmint", "Glowmint", "UV", e, "#BFE6C6", 0.8f, 0.8f, "#6BFF9E");
            Finish("glowlemon", "Glowlemon", "UV", e, "#E9F29B", 0.8f, 0.8f, "#E8FF4D");
            Finish("glowpink", "Glowpink", "UV", e, "#F7C3E6", 0.8f, 0.8f, "#FF6BD5");
            Finish("opal", "Opal", "Holographic", e, "#E7E0F4", 0.8f, 1.5f, null, "#9BE7FF", "#FFB3E6", "#FFF1A8", "#B3FFD1");
            Finish("prism", "Prism", "Holographic", e, "#D8ECF2", 0.8f, 1.5f, null, "#FF9BB3", "#9BD7FF", "#C8FF9B", "#FFE39B");

            Finish("golden_ticket", "Golden Ticket", "Legendary", Rarity.Legendary, "#D9B45A", 0.75f, 2f, null,
                "#FFFFFF", "#FFF2C2", "#FFE08A");
        }


        private static void Finish(string id, string name, string tier, Rarity rarity, string color, float smoothness,
            float riseSeconds, string glow, params string[] sparkles)
        {
            Make<SquishyFinishAsset>("Finishes", id, a =>
            {
                a.def = new SquishyFinishDef { id = id, displayName = name, tier = tier, rarity = rarity };
                a.baseColor = Hex(color);
                a.smoothness = smoothness;
                a.riseSeconds = riseSeconds;
                a.glowColor = glow != null ? Hex(glow) : Color.black;
                a.sparkleColors = new Color[sparkles.Length];
                for (int i = 0; i < sparkles.Length; i++) a.sparkleColors[i] = Hex(sparkles[i]);
            });
        }

        // ---- Kitchen ------------------------------------------------------------------

        private static void SeedKitchen()
        {
            var c = Rarity.Common;
            var r = Rarity.Rare;

            Ingredient("flour", "Flour", c, 8, "#F4EFE4");
            Ingredient("cabbage", "Cabbage", c, 8, "#A7C97E");
            Ingredient("scallion", "Scallion", c, 8, "#7DBA5E");
            Ingredient("ginger", "Ginger", c, 0, "#E1B96A");
            Ingredient("mushroom", "Mushroom", c, 0, "#A0724E");
            Ingredient("prawn", "Prawn", r, 0, "#F08A6A");
            Ingredient("tofu", "Tofu", c, 8, "#F5EEDC");
            Ingredient("egg", "Egg", c, 8, "#F7EBD0");
            Ingredient("sesame", "Sesame", c, 0, "#E9D9B0");
            Ingredient("carrot", "Carrot", c, 8, "#EE8A3A");
            Ingredient("bok_choy", "Bok choy", c, 0, "#6FAE5A");
            Ingredient("chili", "Chili", r, 0, "#D8412F");

            Tool("spatula", "Spatula", c, 18, 60, "#C08A57");
            Tool("cleaver", "Cleaver", r, 30, 0, "#9AA3AB");
            Tool("fork_set", "Fork set", c, 18, 60, "#C9C9C9");
            Tool("wok", "Wok", r, 30, 0, "#3B2A20");
            Tool("rolling_pin", "Rolling pin", r, 30, 0, "#D6AE72");
            Tool("mini_steamer", "Mini steamer", r, 30, 0, "#D6AE72");
            Tool("ladle", "Ladle", c, 18, 60, "#9AA3AB");
            Tool("chopsticks", "Chopsticks", c, 18, 60, "#8A5D3B");

            ToolSkin("classic", "Classic", c, null);
            ToolSkin("copper", "Copper", c, "#C8784A");
            ToolSkin("jade", "Jade", r, "#6FA58E");
            ToolSkin("candy", "Candy", r, "#F4B8C8");
            ToolSkin("gold", "Gold", Rarity.Epic, "#D9B45A");

            // Snack hunger amount isn't in the spec yet (only the 60% cap): 0.25 is a placeholder to tune.
            Snack("rice_cracker", "Rice cracker");
            Snack("apple_slices", "Apple slices");
            Snack("mochi_bite", "Mochi bite");

            Recipe("plain_congee", "Plain congee", 1, 0.6f, S(), S(), false, 0.6f);
            Recipe("scallion_buns", "Scallion buns", 1, 0.40f, S("flour", "scallion"), S("mini_steamer"));
            Recipe("mushroom_omelette", "Mushroom omelette", 1, 0.40f, S("egg", "mushroom"), S("spatula"), true, 1f, NeedKind.Play, 0.10f);
            Recipe("ginger_soup", "Ginger soup", 2, 0.35f, S("ginger", "carrot", "scallion"), S("ladle"), true, 1f, NeedKind.Rest, 0.20f);
            Recipe("sesame_balls", "Sesame balls", 2, 0.30f, S("flour", "sesame"), S("wok"), true, 1f, NeedKind.Play, 0.20f);
            Recipe("veg_dumplings", "Veg dumplings", 2, 0.55f, S("flour", "cabbage", "mushroom"), S("rolling_pin", "mini_steamer"), true, 1f, NeedKind.Rest, 0.10f);
            Recipe("tofu_stir_fry", "Tofu stir-fry", 3, 0.55f, S("tofu", "bok_choy", "chili"), S("wok", "spatula"), true, 1f, NeedKind.Play, 0.15f);
            Recipe("prawn_har_gow", "Prawn har gow", 3, 0.70f, S("flour", "prawn", "scallion"), S("rolling_pin", "mini_steamer", "cleaver"), true, 1f, NeedKind.Clean, 0.10f);
            Recipe("chili_wontons", "Chili wontons", 4, 0.65f, S("flour", "prawn", "chili"), S("cleaver", "rolling_pin", "chopsticks"), true, 1f, NeedKind.Play, 0.25f);
        }

        private static void Ingredient(string id, string name, Rarity rarity, int price, string color)
        {
            Make<IngredientAsset>("Ingredients", id, a =>
            {
                a.def = new IngredientDef { id = id, displayName = name, rarity = rarity, shopPrice = price };
                a.color = Hex(color);
            });
        }

        private static void Tool(string id, string name, Rarity rarity, int uses, int price, string color)
        {
            Make<ToolAsset>("Tools", id, a =>
            {
                a.def = new ToolDef { id = id, displayName = name, rarity = rarity, maxUses = uses, shopPrice = price };
                a.color = Hex(color);
            });
        }

        private static void ToolSkin(string id, string name, Rarity rarity, string tint)
        {
            Make<ToolSkinAsset>("ToolSkins", id, a =>
            {
                a.def = new ToolSkinDef { id = id, displayName = name, rarity = rarity };
                a.useTint = tint != null;
                a.tint = tint != null ? Hex(tint) : Color.white;
            });
        }

        private static void Snack(string id, string name)
        {
            Make<SnackAsset>("Snacks", id, a =>
            {
                a.def = new SnackDef { id = id, displayName = name, shopPrice = 5, hungerAdd = 0.25f, hungerCap = 0.6f };
            });
        }

        private static void Recipe(string id, string name, int kitchenLevel, float hunger, List<string> ingredients,
            List<string> tools, bool countsForTasks = true, float hungerCap = 1f,
            NeedKind bonusNeed = NeedKind.Hunger, float bonus = 0f)
        {
            Make<RecipeAsset>("Recipes", id, a =>
            {
                a.def = new RecipeDef
                {
                    id = id, displayName = name, kitchenLevel = kitchenLevel, hungerAdd = hunger, hungerCap = hungerCap,
                    ingredientIds = ingredients, toolIds = tools, mainToolId = tools.Count > 0 ? tools[0] : null,
                    countsForTasks = countsForTasks,
                };
                if (bonus > 0f) a.def.bonuses.Add(new NeedAmount { need = bonusNeed, amount = bonus });
            });
        }

        // ---- Tasks --------------------------------------------------------------------

        private static void SeedTasks()
        {
            Task("cook_recipe", "Cook a recipe", "cook.recipe", 1, 40);
            Task("scrub", "Scrub your squishy in the bath or shower", "care.scrub", 1, 30);
            Task("fetch", "Play fetch: 5 kicks", "ball.kick", 5, 30);
            Task("nap_dark", "Nap with the lights off", "nap.lightsOff", 1, 30);
            Task("stay_happy", "Stay Happy for 3 minutes", "happy.minute", 3, 50);
            Task("tea_time", "Have tea time", "tea.time", 1, 25);
            Task("squish", "Squish 10 times", "squish", 10, 20);
            Task("water_plant", "Water a plant", "plant.water", 1, 15);
            Task("give_snack", "Give a snack", "snack.give", 1, 15);
        }

        private static void Task(string id, string text, string eventId, int count, int reward)
        {
            Make<TaskAsset>("Tasks", id, a =>
            {
                a.def = new TaskDef { id = id, description = text, eventId = eventId, targetCount = count, coinReward = reward };
            });
        }

        // ---- Helpers ------------------------------------------------------------------

        private static void Make<T>(string folder, string fileName, Action<T> fill) where T : ScriptableObject
        {
            string dir = ContentRoot + "/" + folder;
            string path = dir + "/" + fileName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
            EnsureFolder(dir);
            var asset = ScriptableObject.CreateInstance<T>();
            fill(asset);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static RarityWeight Weight(Rarity rarity, float weight)
        {
            return new RarityWeight { rarity = rarity, weight = weight };
        }

        private static CategoryTable Categories(Rarity rarity, params object[] pairs)
        {
            var table = new CategoryTable { rarity = rarity };
            for (int i = 0; i < pairs.Length; i += 2)
                table.categories.Add(new CategoryWeight { category = (RewardCategory)pairs[i], weight = Convert.ToSingle(pairs[i + 1]) });
            return table;
        }

        private static List<string> S(params string[] ids)
        {
            return new List<string>(ids);
        }

        private static Color Hex(string hex)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(hex, out c)) throw new ArgumentException("Bad colour " + hex);
            return c;
        }

        private static T FindFirst<T>() where T : ScriptableObject
        {
            var all = FindAll<T>();
            if (all.Count > 1) Debug.LogWarning("More than one " + typeof(T).Name + " under " + ContentRoot + "; using " + all[0].name + ".");
            return all.Count > 0 ? all[0] : null;
        }

        private static List<T> FindAll<T>() where T : ScriptableObject
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { ContentRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        private static int CountAssets()
        {
            return Directory.Exists(ContentRoot) ? AssetDatabase.FindAssets("t:ScriptableObject", new[] { ContentRoot }).Length : 0;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
