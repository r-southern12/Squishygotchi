using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Squishy.Runtime.Save;
using Squishy.Simulation.Core;
using Squishy.Simulation.Game;
using Squishy.Simulation.Save;
using UnityEngine;

namespace Squishy.Tests
{
    public class GameRulesTests
    {
        private static GameContent Content()
        {
            var c = JsonUtility.FromJson<GameContent>(File.ReadAllText("Assets/_Project/Resources/Content/game_content.json"));
            c.Init();
            return c;
        }

        [Test]
        public void Growing_Up_Pays_A_Little_Prestige_Once_Per_Stage_Scaled_By_Care()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 3));
            rules.S.prestige = 0;
            rules.S.qolSum = 1; rules.S.qolTime = 2; // quality of life 50%
            Assert.AreEqual(0, rules.AwardStagePrestige(), "a baby has earned nothing yet");
            rules.S.age = (int)c.rules.babyDays + 1; // now young
            Assert.AreEqual((int)System.Math.Round(c.rules.stagePrestige[0] * .5), rules.AwardStagePrestige());
            Assert.AreEqual(0, rules.AwardStagePrestige(), "each stage pays once");
            rules.StartLife(rules.S.favIdx);
            Assert.AreEqual(0, rules.S.stageAwarded, "a new life starts again");
        }

        [Test]
        public void Each_Squishy_Keeps_Its_Own_Life_And_A_Full_Life_Ends_In_A_Baby()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 5));
            var s = rules.S;
            int a = s.favIdx, b = (a + 1) % c.finishes.Length;
            s.squishOwned.Add(new CountData { i = b, n = 1 }); // a second squishy to swap to (a new game starts with one)
            s.age = 20;
            rules.SwapFavourite(b);
            Assert.AreEqual(b, s.favIdx);
            Assert.AreEqual(1, s.age, "a squishy that has never lived starts as a baby");
            s.age = 4;
            rules.SwapFavourite(a);
            Assert.AreEqual(20, s.age, "swapping back resumes its own age");
            rules.SwapFavourite(b);
            Assert.AreEqual(4, s.age);
            int copies = rules.SquishCount(b);
            rules.EndLife(true, "Old age");
            rules.StartLife(b, true);
            Assert.AreEqual(b, s.favIdx);
            Assert.AreEqual(1, s.age, "the baby starts a new life");
            Assert.AreEqual(copies, rules.SquishCount(b), "a full life keeps its copies");
            Assert.IsFalse(s.dead);
        }

        [Test]
        public void Profile_Name_Is_Trimmed_And_Friend_Code_Is_Stable()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 7));
            Assert.AreEqual("Keeper", rules.SetProfile("   ", "#C8674E"));
            Assert.AreEqual(GameRules.NameMax, rules.SetProfile("An extremely long player name", null).Length);
            string code = rules.S.friendCode;
            StringAssert.IsMatch("^[A-Z2-9]{4}-[A-Z2-9]{4}$", code);
            Assert.AreEqual(code, rules.EnsureFriendCode());
            Assert.IsTrue(rules.S.welcomed);
            Assert.AreEqual("#C8674E", rules.S.avatar);
        }

        [Test]
        public void Gacha_10000_Opens_Match_Published_Rates_And_Pity()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 12345));
            var counts = new Dictionary<string, int> { { "Common", 0 }, { "Rare", 0 }, { "Epic", 0 }, { "Legendary", 0 } };
            int sinceRare = 0, sinceEpic = 0, worstRare = 0, worstEpic = 0;
            for (int n = 0; n < 10000; n++)
            {
                var r = rules.RollReward();
                counts[r.rar]++;
                int rank = GameContent.RarityRank(r.rar);
                sinceRare = rank >= 1 ? 0 : sinceRare + 1;
                sinceEpic = rank >= 2 ? 0 : sinceEpic + 1;
                worstRare = Math.Max(worstRare, sinceRare);
                worstEpic = Math.Max(worstEpic, sinceEpic);
            }
            // Pity lifts the effective rates a little above the base 76/18/5/1.
            Assert.That(counts["Common"] / 10000.0, Is.InRange(0.66, 0.78));
            Assert.That(counts["Rare"] / 10000.0, Is.InRange(0.16, 0.26));
            Assert.That(counts["Epic"] / 10000.0, Is.InRange(0.04, 0.08));
            Assert.That(counts["Legendary"] / 10000.0, Is.InRange(0.005, 0.02));
            Assert.That(worstRare, Is.LessThan(10), "Rare or better within 10");
            Assert.That(worstEpic, Is.LessThan(50), "Epic or better within 50");
        }

        [Test]
        public void Gacha_Is_Deterministic_For_A_Seed()
        {
            var c = Content();
            var a = new GameRules(c, GameRules.NewState(c, 7));
            var b = new GameRules(c, GameRules.NewState(c, 7));
            for (int n = 0; n < 200; n++) Assert.AreEqual(a.RollReward().rar, b.RollReward().rar);
        }

        [Test]
        public void Needs_Drain_Slower_With_Comfort_And_Neglect_Kills()
        {
            var c = Content();
            var s = GameRules.NewState(c, 1);
            var rules = new GameRules(c, s);
            float before = s.needs[Needs.Hunger];
            rules.StepCare(100f, 0f);
            float plain = before - s.needs[Needs.Hunger];
            s.needs[Needs.Hunger] = before;
            rules.StepCare(100f, 20f);
            Assert.AreEqual(plain * 0.6f, before - s.needs[Needs.Hunger], 1e-4f, "comfort 20 slows drain by the 40% cap");
            s.needs[Needs.Play] = 0f;
            Assert.IsFalse(rules.StepCare(c.rules.deathSeconds - 10f, 0f));
            Assert.IsTrue(rules.StepCare(20f, 0f), "an empty need for too long kills");
        }

        [Test]
        public void Good_Care_Lives_Longer_And_Earns_More_Prestige()
        {
            var c = Content();
            var s = GameRules.NewState(c, 1);
            var rules = new GameRules(c, s);
            s.qolSum = .9f; s.qolTime = 1;
            float good = rules.ExpectedLifespanDays();
            s.qolSum = .3f;
            Assert.Less(rules.ExpectedLifespanDays(), good);
            s.age = 1; Assert.AreEqual(GameRules.Life.Baby, rules.LifeStage());
            s.qolSum = .9f; s.age = 100;
            Assert.IsTrue(rules.ReachedOldAge());
            var rec = rules.EndLife(true, "Old age");
            Assert.AreEqual((int)Math.Round(c.rules.prestigeBase + c.rules.prestigePerQol * .9f), rec.prestige);
            Assert.AreEqual(rec.prestige, s.prestige);
            Assert.IsTrue(rules.TrialOver(), "free players get one life");
            s.premium = true;
            Assert.IsFalse(rules.TrialOver());
        }

        [Test]
        public void Tucked_In_Pauses_Needs_And_Death()
        {
            var c = Content();
            var s = GameRules.NewState(c, 1);
            var rules = new GameRules(c, s);
            Assert.IsTrue(rules.CanTuck());
            s.tucked = true;
            float h = s.needs[Needs.Hunger];
            s.needs[Needs.Play] = 0f;
            Assert.IsFalse(rules.StepCare(100000f, 0f), "no death while tucked in");
            Assert.AreEqual(h, s.needs[Needs.Hunger], "no drain while tucked in");
            s.tucked = false;
            Assert.IsFalse(rules.CanTuck(), "can't tuck in while Critical");
        }

        [Test]
        public void Comfort_Counts_Wilt_And_Set_Bonus()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 1));
            string set;
            var items = new List<PieceState>
            {
                new PieceState { key = "plant:cottage", wilt = 0.5f }, new PieceState { key = "lamp:cottage" }, new PieceState { key = "bed:cottage" },
            };
            Assert.AreEqual(1f + 2f + 2f + 3f, rules.Comfort(items, out set), 1e-4f);
            Assert.AreEqual("Cottage", set);
        }

        [Test]
        public void Tasks_Pay_And_Three_Give_A_Steamer()
        {
            var c = Content();
            var s = GameRules.NewState(c, 3);
            var clock = new ManualClock(new DateTime(2026, 9, 25, 9, 0, 0, DateTimeKind.Local).ToUniversalTime()); // 9am local: all three on one day (no streak bonus)
            var rules = new GameRules(c, s) { Clock = clock };
            int coins = s.coins, steamers = s.steamers, paid = 0;
            for (int k = 0; k < 3; k++)
            {
                var t = s.tasks[0];
                var d = rules.TaskDef(t);
                rules.TaskEvent(GameRules.Ev(d), d.goal);
                paid += d.coins;
                rules.ClaimTask(0);
                Assert.IsFalse(rules.TaskReady(s.tasks[0]), "a claimed slot rests before its next task");
                clock.Advance(TimeSpan.FromHours(c.rules.taskCooldownHours));
            }
            Assert.AreEqual(coins + paid, s.coins);
            Assert.AreEqual(steamers + 3 * c.rules.taskSteamers, s.steamers, "a steamer per task and nothing more");
        }

        [Test]
        public void Recipes_Need_Ingredients_Tools_And_Kitchen_Level()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 1));
            Assert.IsEmpty(rules.MissingFor(c.recipes[0]), "congee is always free");
            CollectionAssert.Contains(rules.MissingFor(c.recipes[2]), "Wok", "pork & chive stir-fry needs a wok, which starts in steamers");
            int scramble = System.Array.FindIndex(c.recipes, r => r.name == "Egg & chive scramble");
            Assert.IsEmpty(rules.MissingFor(c.recipes[scramble]), "egg & chive scramble: egg, chives, spatula (all in the starter kitchen)");
            CollectionAssert.Contains(rules.MissingFor(c.recipes[16]), "Kitchen Lv 4");
            // Only starter recipes are known at first; the rest come from kitchen kits.
            Assert.IsTrue(rules.Knows(0) && rules.Knows(2));
            Assert.IsFalse(rules.Knows(1), "char siu pork is learned from a kit");
            for (int i = 0; i < c.recipes.Length; i++) Assert.AreEqual(c.recipes[i].starter, rules.Knows(i), c.recipes[i].name);
        }

        [Test]
        public void Each_Steamer_Skin_Unlocks_Its_Music()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 1));
            foreach (var k in c.skins) Assert.IsFalse(string.IsNullOrEmpty(k.music), k.name + " has a track");
            CollectionAssert.AreEqual(new[] { 0 }, rules.UnlockedTracks(), "the Bamboo steamer's track at first");
            rules.AddOwned("skin:3");
            CollectionAssert.AreEqual(new[] { 0, 3 }, rules.UnlockedTracks());
        }

        [Test]
        public void Shop_Sells_Common_Ingredients_One_At_A_Time_Never_Rare()
        {
            var c = Content();
            var s = GameRules.NewState(c, 5);
            var rules = new GameRules(c, s);
            s.coins = 1000;
            int common = System.Array.FindIndex(c.pantry, p => p.rarity == "Common"), rare = System.Array.FindIndex(c.pantry, p => p.rarity != "Common");
            int had = s.pantry[common];
            Assert.IsTrue(rules.BuyIngredient(common));
            Assert.AreEqual(had + 1, s.pantry[common]);
            Assert.AreEqual(1000 - c.rules.ingredientPrice, s.coins);
            Assert.IsFalse(rules.BuyIngredient(rare), "rare ingredients are never sold");
            var rc = System.Array.Find(c.recipes, r => System.Array.Exists(r.ing, i => c.pantry[i].rarity != "Common"));
            foreach (var i in rc.ing) s.pantry[i] = 0;
            var lc = new System.Collections.Generic.List<int>(); var lr = new System.Collections.Generic.List<int>();
            rules.MissingIngredients(rc, lc, lr);
            Assert.IsTrue(lr.Count > 0 && lr.TrueForAll(i => !rules.ShopSells(i)) && lc.TrueForAll(i => rules.ShopSells(i)));
        }

        [Test]
        public void Combos_Form_From_Nearby_Pieces_And_Share_Them()
        {
            var c = Content();
            var s = GameRules.NewState(c, 7);
            var rules = new GameRules(c, s);
            string sty = c.styles[0].id;
            var room = new System.Collections.Generic.List<PieceState>
            {
                new PieceState { key = "teatable:" + sty, x = 0, z = 0 },
                new PieceState { key = "chair:" + sty, x = .6f, z = 0 },
                new PieceState { key = "cushion:" + sty, x = -.6f, z = 0 },
                new PieceState { key = "stove:" + sty, x = 0, z = .8f },
                new PieceState { key = "plant:" + sty, x = 3, z = 3 },
            };
            var all = rules.Combos(room);
            var tea = all.Find(m => m.combo.id == "tea_time");
            var dinner = all.Find(m => m.combo.id == "dinner_table");
            Assert.IsTrue(tea.Done, "table + stool + cushion is tea time");
            Assert.IsTrue(dinner.Done, "the same table and stool with a stove near is the dinner table");
            Assert.Contains(room[1], dinner.pieces, "pieces are shared between combos");
            var spa = all.Find(m => m.combo.id == "spa_bath");
            Assert.AreEqual(1, spa.have, "a lone plant is 1 of 2 towards the spa bath");
            Assert.IsNull(spa.pieces[0], "the missing piece stays unknown");
            // Near is right next to: a tub and a plant a metre apart are not a spa bath; side by side they are.
            room.Add(new PieceState { key = "tub:" + sty, x = -3, z = -3 });
            room.Add(new PieceState { key = "plant:" + sty, x = -2, z = -3 });
            Assert.AreEqual(1, rules.Combos(room).Find(m => m.combo.id == "spa_bath").have, "a metre apart is too far");
            room[room.Count - 1].x = -2.45f;
            Assert.IsTrue(rules.Combos(room).Find(m => m.combo.id == "spa_bath").Done, "side by side");
            room[2].x = 3; // the cushion moves away: tea time is 2 of 3
            tea = rules.Combos(room).Find(m => m.combo.id == "tea_time");
            Assert.AreEqual(2, tea.have);
            Assert.IsFalse(tea.Done);
            Assert.AreEqual(1, rules.NewlyFound(all).FindAll(x => x.id == "tea_time").Count);
            Assert.AreEqual(0, rules.NewlyFound(all).Count, "each combo is celebrated once");
        }

        [Test]
        public void New_Combos_Are_Found_Two_Plants_Make_A_Greenhouse()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 1));
            string sty = c.styles[0].id;
            var room = new System.Collections.Generic.List<PieceState>
            {
                new PieceState { key = "plant:" + sty, x = 0, z = 0 },
                new PieceState { key = "lamp:" + sty, x = .4f, z = 0 },
                new PieceState { key = "tub:" + sty, x = -2, z = 2 },
                new PieceState { key = "bubbles:" + sty, x = -1.5f, z = 2 },
                new PieceState { key = "xylophone:" + sty, x = 2, z = -2 },
                new PieceState { key = "pomwand:" + sty, x = 2.45f, z = -2 },
            };
            var all = rules.Combos(room);
            Assert.AreEqual(2, all.Find(m => m.combo.id == "greenhouse").have, "one plant and a lamp: 2 of 3");
            Assert.IsTrue(all.Find(m => m.combo.id == "bubble_bath").Done);
            Assert.IsTrue(all.Find(m => m.combo.id == "pompom_dance").Done);
            room.Add(new PieceState { key = "plant:" + sty, x = -.4f, z = 0 });
            var gh = rules.Combos(room).Find(m => m.combo.id == "greenhouse");
            Assert.IsTrue(gh.Done, "a second plant beside them");
            Assert.AreEqual(.5f, gh.combo.wiltMul, 1e-6);
        }

        [Test]
        public void Room_Holds_One_More_Piece_Per_Level_Up_To_30_And_Widens_With_Size()
        {
            var c = Content();
            var s = GameRules.NewState(c, 11);
            var rules = new GameRules(c, s);
            Assert.AreEqual(15, rules.ItemSlots(), "the starter room holds 15 pieces (12 placed, 3 spare)");
            Assert.AreEqual(30, c.roomLevels[c.roomLevels.Length - 1].slots, "30 at most");
            for (int i = 1; i < c.roomLevels.Length; i++) Assert.AreEqual(c.roomLevels[i - 1].slots + 1, c.roomLevels[i].slots, "one more piece per level");
            for (int i = 2; i < c.roomLevels.Length; i++) Assert.Greater(c.roomLevels[i].cost, c.roomLevels[i - 1].cost, "slower and slower");
            // Room levels go as far as this steamer allows; then the next steamer size must be bought.
            for (int i = 0; i < c.finishes.Length && i < 30; i++) if (rules.SquishCount(i) == 0) s.squishOwned.Add(new CountData { i = i, n = 1 });
            s.coins = 100000;
            int cap = rules.SlotCap();
            s.roomLv = System.Array.FindIndex(c.roomLevels, l => l.slots == cap);
            Assert.IsFalse(rules.CanExpand(), "full for the Small steamer");
            Assert.IsTrue(rules.ExpandNeedsBiggerSteamer());
            float r0 = rules.RoomRadius();
            s.coins = c.steamerSizes[1].coins;
            Assert.IsTrue(rules.BuySteamerSize(), "Medium is bought with coins");
            Assert.AreEqual(0, s.coins);
            Assert.Greater(rules.RoomRadius(), r0, "and the steamer is wider");
            s.coins = 100000;
            s.prestige = 100000;
            Assert.IsTrue(rules.CanExpand(), "room levels carry on");
            Assert.IsFalse(rules.BuySteamerSize(), "Large can't be bought, with coins or prestige");
            Assert.AreEqual(1, rules.RoomSizeIdx);
            s.lives.Add(new LifeRecord { cause = "Neglect" });
            Assert.AreEqual(1, rules.RoomSizeIdx, "only a full life counts");
            s.lives.Add(new LifeRecord { cause = "Old age" });
            Assert.AreEqual(2, rules.RoomSizeIdx, "Large is the reward for a full life");
            s.lives.Add(new LifeRecord { cause = "Old age" });
            Assert.AreEqual(3, rules.RoomSizeIdx, "Extra large for two");
            var s2 = GameRules.NewState(c, 5);
            var r2 = new GameRules(c, s2);
            s2.lives.Add(new LifeRecord { cause = "Old age" });
            Assert.AreEqual(0, r2.RoomSizeIdx, "the reward waits for Medium");
            s2.coins = c.steamerSizes[1].coins;
            Assert.IsTrue(r2.BuySteamerSize());
            Assert.AreEqual(2, r2.RoomSizeIdx, "and comes straight after it");
            Assert.AreEqual(c.steamerSizes[c.steamerSizes.Length - 1].maxSlots, c.roomLevels[c.roomLevels.Length - 1].slots, "the biggest steamer reaches the last level");
        }

        [Test]
        public void Missions_Goal_Starts_Afresh_Monday_And_Thursday()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 2));
            var wed = new DateTime(2026, 9, 30); // a Wednesday
            Assert.AreEqual(DayOfWeek.Monday, rules.GoalStart(wed).DayOfWeek, "Wednesday is still Monday's goal");
            Assert.AreEqual(DayOfWeek.Thursday, rules.GoalStart(wed.AddDays(1)).DayOfWeek, "Thursday starts a new one");
            Assert.AreEqual(DayOfWeek.Thursday, rules.GoalStart(wed.AddDays(4)).DayOfWeek, "through Sunday");
            Assert.AreEqual(10, c.rules.goalSteamers);
            foreach (var t in c.tierRewards) { Assert.AreEqual(0, t.steamers, "tree tiers pay prestige, not steamers"); Assert.Greater(t.prestige, 0); }
        }

        [Test]
        public void Streak_Is_A_Coin_Bonus_Not_Steamers()
        {
            var c = Content();
            var s = GameRules.NewState(c, 4);
            var clock = new ManualClock(new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc));
            var rules = new GameRules(c, s) { Clock = clock };
            var d = c.tasks[0];
            for (int day = 0; day < 8; day++)
            {
                int steamers = s.steamers;
                rules.RecordTaskDone();
                if (day < 6) Assert.AreEqual(steamers, s.steamers - (s.weekClaimed && s.weekTasks == c.rules.goalMissions ? c.rules.goalSteamers : 0), "streak days pay no steamers");
                clock.Advance(TimeSpan.FromDays(1));
            }
            clock.Advance(TimeSpan.FromDays(-1));
            Assert.AreEqual(c.rules.streakBonusMax, rules.StreakBonus(), 1e-5, "capped");
            Assert.AreEqual((int)Math.Round(d.coins * (1 + c.rules.streakBonusMax)), rules.TaskCoins(d));
            clock.Advance(TimeSpan.FromDays(3));
            Assert.AreEqual(0, rules.StreakBonus(), 1e-5, "a missed day breaks it");
        }

        [Test]
        public void Asleep_Rest_Fills_While_Others_Drain_Slowly()
        {
            var c = Content();
            var s = GameRules.NewState(c, 8);
            s.age = (int)c.rules.babyDays + 2; // past the Baby stage (babies drain faster)
            var utc = new DateTime(2026, 9, 28, 11, 0, 0, DateTimeKind.Utc);
            var rules = new GameRules(c, s) { Clock = new ManualClock(utc) };
            s.needs = new[] { .8f, .8f, .2f, .8f };
            rules.GoToSleep(utc, new DateTime(2026, 9, 28, 21, 0, 0));
            for (int i = 0; i < 6 * 60; i++) // six hours, a minute at a time
            {
                rules.DrainScale = rules.DrainScaleAt(utc.AddMinutes(i).Ticks);
                rules.StepCare(60, 0);
            }
            Assert.Greater(s.needs[Needs.Rest], .95f, "a night's sleep fills Rest");
            Assert.Less(s.needs[Needs.Hunger], .8f, "the others still drain");
            Assert.Greater(s.needs[Needs.Hunger], .8f - c.rules.decayHunger * 6 * 3600 * .5f, "but slowly");
            // Tucked in: everything pauses except Rest.
            var t = GameRules.NewState(c, 9);
            var tr = new GameRules(c, t);
            t.needs = new[] { .5f, .5f, .2f, .5f };
            t.tucked = true;
            tr.StepCare(3600, 0);
            Assert.AreEqual(.5f, t.needs[Needs.Hunger], 1e-5);
            Assert.Greater(t.needs[Needs.Rest], .2f);
        }

        [Test]
        public void New_Game_Starts_Bare_With_Random_Common_Pieces()
        {
            var c = Content();
            var s = GameRules.NewState(c, 21);
            var rules = new GameRules(c, s);
            Assert.AreEqual(0, s.coins);
            Assert.AreEqual(0, s.steamers);
            Assert.AreEqual(1, s.squishOwned.Count, "one squishy");
            Assert.IsFalse(s.owned.Exists(k => k.StartsWith("sink:")), "no sink to start");
            Assert.IsFalse(s.owned.Exists(k => k.StartsWith("tool:") && k != "tool:0"), "spatula only");
            Assert.IsFalse(s.owned.Exists(k => k == "skin:1"), "Bamboo steamer only");
            foreach (var t in new[] { "pomwand", "bubbles", "xylophone", "slide" }) Assert.IsFalse(s.owned.Exists(k => k.StartsWith(t + ":")), "the ball is the only toy");
            var keys = new System.Collections.Generic.List<string>(s.storage);
            foreach (var p in s.items) keys.Add(p.key);
            foreach (var k in keys)
            {
                Assert.AreEqual("Common", c.Catalogue.Find(x => x.key == k).rarity, k + " is Common");
                Assert.IsTrue(rules.Owned(k), k + " is owned");
            }
            var other = GameRules.NewState(c, 99);
            bool differs = false;
            for (int i = 0; i < s.items.Count; i++) differs |= s.items[i].key != other.items[i].key;
            Assert.IsTrue(differs, "a different start each new game");
        }

        [Test]
        public void One_More_Toy_Out_Each_Time_The_Steamer_Grows()
        {
            var c = Content();
            var s = GameRules.NewState(c, 3);
            var rules = new GameRules(c, s);
            Assert.AreEqual(1, rules.ToySlots(), "one toy in the smallest steamer");
            for (int i = 1; i < c.steamerSizes.Length; i++)
            {
                s.roomSize = i;
                Assert.AreEqual(i + 1, rules.ToySlots(), c.steamerSizes[i].name + ": one more each time the steamer grows");
            }
        }

        [Test]
        public void A_Pity_Pull_Is_Always_A_Squishy()
        {
            var c = Content();
            int pity = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var s = GameRules.NewState(c, (ulong)seed);
                var rules = new GameRules(c, s);
                s.sinceRare = c.rules.pityRare - 1;
                var rw = rules.RollReward();
                if (rules.LastWasPity) { pity++; Assert.AreEqual("sq", rw.type, "Rare pity gives a squishy"); }
                s.sinceEpic = c.rules.pityEpic - 1;
                rw = rules.RollReward();
                if (rules.LastWasPity) { pity++; Assert.AreEqual("sq", rw.type, "Epic pity gives a squishy"); } // (a natural Epic or better needs no pity)
            }
            Assert.Greater(pity, 20, "pity stepped in");
        }

        [Test]
        public void Kits_Only_Teach_Recipes_You_Dont_Know()
        {
            var c = Content();
            var s = GameRules.NewState(c, 17);
            var rules = new GameRules(c, s);
            for (int n = 0; n < 2000; n++)
            {
                var rw = rules.RollReward();
                if (rw.type == "kit") Assert.IsFalse(rules.Knows(rw.i), "a kit for a known recipe: " + c.recipes[rw.i].name);
            }
            for (int i = 0; i < c.recipes.Length; i++) rules.AddOwned(rules.RecipeKey(i));
            for (int n = 0; n < 2000; n++) Assert.AreNotEqual("kit", rules.RollReward().type, "every recipe known: no more kits");
        }

        [Test]
        public void Neighbours_Stop_At_The_Limit_Friends_Do_Not()
        {
            var c = Content();
            var s = GameRules.NewState(c, 1);
            var rules = new GameRules(c, s);
            var snap = rules.Snapshot();
            for (int i = 0; i < 30; i++) rules.RememberFriend("friend" + i, snap, true);
            for (int i = 0; i < c.rules.neighbourMax; i++) Assert.IsTrue(rules.AddNeighbour("n" + i, snap));
            Assert.IsFalse(rules.AddNeighbour("one_more", snap), "neighbours are capped");
            Assert.IsFalse(rules.AddNeighbour("n0", snap), "no doubles");
            Assert.AreEqual(c.rules.neighbourMax, rules.NeighbourCount());
            rules.RememberFriend("friend30", snap, true);
            Assert.AreEqual(31 + c.rules.neighbourMax, s.friends.Count, "friends by code have no limit");
            rules.RememberFriend("n0", snap, true);
            Assert.AreEqual(c.rules.neighbourMax - 1, rules.NeighbourCount(), "adding a neighbour by code makes them a friend");
            rules.RemoveFriend("n1");
            Assert.IsTrue(rules.AddNeighbour("new", snap));
        }

        [Test]
        public void Happy_Income_Only_While_Every_Need_Is_Over_Half()
        {
            var c = Content();
            var s = GameRules.NewState(c, 1);
            var rules = new GameRules(c, s);
            for (int k = 0; k < 4; k++) s.needs[k] = .9f;
            int coins0 = s.coins;
            int paid = 0;
            for (int i = 0; i < 360; i++) paid += rules.StepHappy(10, 0); // an hour
            Assert.AreEqual(Mathf.RoundToInt(c.rules.happyBase * 60), paid, 1);
            Assert.AreEqual(coins0 + paid, s.coins);
            s.needs[2] = .4f;
            Assert.AreEqual(0, rules.StepHappy(3600, 0), "not Happy: nothing");
            s.needs[2] = .9f;
            s.tucked = true;
            Assert.AreEqual(0, rules.StepHappy(3600, 0), "paused for a holiday: nothing");
        }

        [Test]
        public void Each_Friend_Can_Be_Visited_Once_Every_Few_Hours()
        {
            var c = Content();
            var s = GameRules.NewState(c, 1);
            var clock = new ManualClock(new System.DateTime(2026, 10, 2, 12, 0, 0));
            var rules = new GameRules(c, s) { Clock = clock };
            var snap = rules.Snapshot();
            rules.RememberFriend("a", snap, true);
            rules.RememberFriend("b", snap, true);
            Assert.AreEqual(System.TimeSpan.Zero, rules.VisitWait("a"), "never visited: go any time");
            rules.MarkVisited("a");
            Assert.AreEqual(c.rules.visitCooldownHours, rules.VisitWait("a").TotalHours, 1e-6);
            Assert.AreEqual(System.TimeSpan.Zero, rules.VisitWait("b"), "each friend has their own wait");
            clock.Advance(System.TimeSpan.FromHours(c.rules.visitCooldownHours - .5));
            Assert.AreEqual(30, rules.VisitWait("a").TotalMinutes, 1e-6);
            clock.Advance(System.TimeSpan.FromMinutes(31));
            Assert.AreEqual(System.TimeSpan.Zero, rules.VisitWait("a"));
        }

        [Test]
        public void Full_Game_Collects_Twice_As_Many_Overnight()
        {
            var c = Content();
            var utc = new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc);
            var local = utc.ToLocalTime().Date.AddHours(22.5); // 10:30pm local
            utc = local.ToUniversalTime();
            int[] got = new int[2];
            for (int k = 0; k < 2; k++)
            {
                var s = GameRules.NewState(c, 1);
                s.premium = k == 1;
                var rules = new GameRules(c, s);
                s.onlineReadyAt = s.bonusReadyAt = utc.AddHours(1).Ticks; // nothing waiting at bedtime
                rules.GoToSleep(utc, local);
                got[k] = rules.WakeUp(utc.AddHours(9.5)).steamers;
            }
            Assert.Greater(got[0], 0);
            Assert.AreEqual(got[0] * 2, got[1], "the bonus steamer is collected overnight too with the full game");
        }

        [Test]
        public void Accessory_Colours_Are_Picked_From_Its_Choices_Once_Owned()
        {
            var c = Content();
            var s = GameRules.NewState(c, 1);
            var rules = new GameRules(c, s);
            var tie = rules.Cosmetic("tie");
            Assert.IsNotNull(tie);
            Assert.AreEqual(tie.color, rules.CosmeticColor("tie"), "its own colour to start");
            Assert.IsFalse(rules.SetCosmeticColor("tie", "#E86A92"), "not owned yet");
            s.cosmetics.Add("tie");
            Assert.IsTrue(rules.SetCosmeticColor("tie", "#E86A92"));
            Assert.AreEqual("#E86A92", rules.Worn("tie").color);
            Assert.AreEqual(tie.color, rules.Cosmetic("tie").color, "the content itself is untouched");
            Assert.IsFalse(rules.SetCosmeticColor("tie", "#123456"), "only its choices");
            Assert.IsTrue(rules.SetCosmeticColor("tie", tie.color), "and back again, any time");
            Assert.AreEqual(0, s.cosColors.Count);
            s.cosmetics.Add("pearls");
            Assert.IsFalse(rules.SetCosmeticColor("pearls", "#E86A92"), "pearls have their own short list");
            Assert.IsTrue(rules.SetCosmeticColor("pearls", "#2B2B2B"));
        }

        [Test]
        public void Colour_Variants_Merge_Into_One_Accessory_In_That_Colour()
        {
            var c = Content();
            var s = GameRules.NewState(c, 1);
            s.cosmetics.AddRange(new[] { "beret", "beret_navy", "straw_hat" });
            s.hat = "straw_hat";
            s.prestige = 0;
            var data = new Squishy.Simulation.Save.SaveData { version = 13, state = s };
            Squishy.Simulation.Save.SaveMigrator.CreateDefault().Migrate(data);
            var rules = new GameRules(c, s);
            Assert.IsFalse(s.cosmetics.Contains("beret_navy") || s.cosmetics.Contains("straw_hat"));
            Assert.IsTrue(rules.HasCosmetic("sun_hat"));
            Assert.AreEqual("sun_hat", s.hat, "still wearing it");
            Assert.AreEqual("#E1B96A", rules.Worn("sun_hat").color, "in the straw colour");
            Assert.AreEqual(20, s.prestige, "owning both berets refunds the navy one");
            Assert.AreEqual(c.cosmetics.Length, new System.Collections.Generic.HashSet<string>(System.Array.ConvertAll(c.cosmetics, x => x.id)).Count);
            Assert.IsNull(rules.Cosmetic("party_hat_teal"), "the variants are gone from the store");
        }

        [Test]
        public void A_Need_Left_Empty_Half_An_Hour_Is_One_Care_Mistake()
        {
            var c = Content();
            var s = GameRules.NewState(c, 2);
            var rules = new GameRules(c, s);
            for (int k = 0; k < 4; k++) s.needs[k] = 1;
            s.needs[Needs.Hunger] = 0;
            float before = rules.ProjectedPrestige();
            for (int i = 0; i < 29; i++) rules.StepCare(60, 0);
            Assert.AreEqual(0, s.careMistakes, "not yet: under half an hour");
            for (int i = 0; i < 60; i++) rules.StepCare(60, 0);
            Assert.AreEqual(1, s.careMistakes, "one mistake, however long it stays empty");
            Assert.AreEqual(1, rules.NewMistakes);
            s.needs[Needs.Hunger] = .05f;
            rules.StepCare(1, 0);
            s.needs[Needs.Hunger] = 0;
            for (int i = 0; i < 40; i++) rules.StepCare(60, 0);
            Assert.AreEqual(1, s.careMistakes, "a crumb doesn't reset it: it has to be looked after");
            s.needs[Needs.Hunger] = .5f;
            rules.StepCare(1, 0);
            s.needs[Needs.Hunger] = 0;
            rules.RestFill = true; // asleep for the night doesn't count
            for (int i = 0; i < 40; i++) { rules.RestFill = true; rules.StepCare(60, 0); }
            Assert.AreEqual(1, s.careMistakes);
            Assert.AreEqual(1 - c.rules.careMistakePenalty, rules.MistakeFactor(), 1e-5);
            s.careMistakes = 40;
            Assert.AreEqual(c.rules.careMistakeFloor, rules.MistakeFactor(), 1e-5, "never below half");
        }

        [Test]
        public void Babies_Need_Looking_After_More()
        {
            var c = Content();
            var s = GameRules.NewState(c, 4);
            var rules = new GameRules(c, s);
            s.age = 1;
            Assert.AreEqual(GameRules.Life.Baby, rules.LifeStage());
            for (int k = 0; k < 4; k++) s.needs[k] = 1;
            rules.StepCare(3600, 0);
            float babyLoss = (1 - s.needs[Needs.Hunger]) / rules.NeedMul(Needs.Hunger);
            s.age = (int)c.rules.babyDays + 2;
            for (int k = 0; k < 4; k++) s.needs[k] = 1;
            rules.StepCare(3600, 0);
            float laterLoss = (1 - s.needs[Needs.Hunger]) / rules.NeedMul(Needs.Hunger);
            Assert.AreEqual(c.rules.babyDrain, babyLoss / laterLoss, .01, "a baby's needs drain faster");
        }

        [Test]
        public void Each_Squishy_Has_Its_Own_Appetite_Per_Stage()
        {
            var c = Content();
            var s = GameRules.NewState(c, 9);
            var rules = new GameRules(c, s);
            var muls = new float[4];
            bool differ = false;
            for (int k = 0; k < 4; k++)
            {
                muls[k] = rules.NeedMul(k);
                Assert.That(muls[k], Is.InRange(1 - c.rules.needVariance, 1 + c.rules.needVariance));
                Assert.AreEqual(muls[k], rules.NeedMul(k), "fixed within a stage");
                if (k > 0 && System.Math.Abs(muls[k] - muls[0]) > .01f) differ = true;
            }
            Assert.IsTrue(differ, "some needs drain faster than others");
            s.age = (int)c.rules.babyDays + 2;
            bool changed = false;
            for (int k = 0; k < 4; k++) if (System.Math.Abs(rules.NeedMul(k) - muls[k]) > .001f) changed = true;
            Assert.IsTrue(changed, "and it changes as it grows up");
        }

        [Test]
        public void A_Visit_Can_Play_Pamper_And_Leave_A_Sticker()
        {
            var c = Content();
            var s = GameRules.NewState(c, 3);
            var rules = new GameRules(c, s);
            s.needs = new[] { .5f, .5f, .5f, .5f };
            var g = rules.CreditVisit("friend1", 1000, 5, "pet,play,pamper,sticker:star", "Nebula");
            Assert.IsNotNull(g);
            Assert.Greater(g.together, 0);
            Assert.Greater(g.clean, 0);
            Assert.AreEqual("star", g.sticker);
            Assert.AreEqual(1, s.stickers.Count);
            Assert.AreEqual("Nebula", s.stickers[0].from);
            Assert.AreEqual(5 * c.rules.visitHostCoins, g.coins, "each caring thing pays the host");
            Assert.IsNull(rules.CreditVisit("friend1", 1000, 5, "pet", "Nebula"), "a visit counts once");
        }

        [Test]
        public void Resets_Fall_On_The_Clock()
        {
            // 3-hour resets land at 0, 3, 6... local, so a claim at 4:50 waits only until 6:00.
            var at = new DateTime(2026, 9, 28, 4, 50, 0, DateTimeKind.Local).ToUniversalTime();
            var next = new DateTime(GameRules.NextReset(at.Ticks, 3), DateTimeKind.Utc).ToLocalTime();
            Assert.AreEqual(6, next.Hour);
            Assert.AreEqual(0, next.Minute);
            Assert.AreEqual(3, GameRules.ResetsBetween(at.Ticks, at.AddHours(9).Ticks, 3), "6, 9 and 12 o'clock");
        }

        [Test]
        public void Free_Steamer_And_Bonus_Every_Few_Hours_Nothing_On_Its_Own()
        {
            var c = Content();
            var s = GameRules.NewState(c, 9);
            var clock = new ManualClock(new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc));
            var rules = new GameRules(c, s) { Clock = clock };
            int before = s.steamers;
            clock.Advance(TimeSpan.FromDays(30));
            Assert.AreEqual(before, s.steamers, "time away alone brings no steamers");
            // In the game: one free, and one more for an optional video, each once per wait (and they don't stack).
            Assert.IsTrue(rules.ClaimOnline());
            Assert.IsFalse(rules.ClaimOnline());
            Assert.IsTrue(rules.ClaimBonus());
            Assert.IsFalse(rules.ClaimBonus());
            clock.Advance(TimeSpan.FromHours(c.rules.giftHours));
            Assert.IsTrue(rules.ClaimOnline() && rules.ClaimBonus());
            Assert.AreEqual(before + 4, s.steamers);
        }

        [Test]
        public void Squishy_Prizes_Are_New_Until_A_Rarity_Is_Complete()
        {
            var c = Content();
            var s = GameRules.NewState(c, 21);
            var rules = new GameRules(c, s);
            for (int n = 0; n < 4000; n++)
            {
                var rw = rules.RollReward();
                if (rw.type != "sq" || rw.i == s.favIdx) continue;
                if (rules.SquishCount(rw.i) > 0)
                {
                    // A repeat (not the favourite) only once every squishy of that rarity is owned.
                    for (int k = 0; k < c.finishes.Length; k++)
                        if (c.FinishRarity(c.finishes[k]) == rw.rar) Assert.Greater(rules.SquishCount(k), 0, c.finishes[k].name + " was still missing");
                }
                rules.Claim(rw);
            }
        }

        [Test]
        public void Pity_Guarantees_Every_Tier()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 77));
            int rare = 0, epic = 0, leg = 0;
            for (int n = 0; n < 30000; n++)
            {
                int k = GameContent.RarityRank(rules.RollRarity());
                rare = k >= 1 ? 0 : rare + 1;
                epic = k >= 2 ? 0 : epic + 1;
                leg = k >= 3 ? 0 : leg + 1;
                Assert.Less(rare, c.rules.pityRare);
                Assert.Less(epic, c.rules.pityEpic);
                Assert.Less(leg, c.rules.pityLegendary);
            }
        }

        [Test]
        public void Published_Odds_Match_The_Rules()
        {
            var c = Content();
            var o = GameRules.MeasureOdds(c);
            Assert.AreEqual(1f, o.common + o.rare + o.epic + o.legendary, .001f);
            Assert.Greater(o.legendary, c.rules.pLegendary, "Legendary pity lifts it above its base rate");
            Assert.Less(o.legendary, c.rules.pLegendary * 2);
            Assert.Greater(o.rare, c.rules.pRare, "pity lifts Rare above its base rate");
            Assert.AreEqual(c.rules.favouriteChance, o.favouriteCopy, .03f);
        }

        [Test]
        public void Night_Sleep_Drains_Slowly_And_Collects_Free_Steamers()
        {
            var c = Content();
            var s = GameRules.NewState(c, 31);
            var utc = new DateTime(2026, 9, 28, 11, 0, 0, DateTimeKind.Utc);
            var clock = new ManualClock(utc);
            var rules = new GameRules(c, s) { Clock = clock };
            var local = new DateTime(2026, 9, 28, 21, 0, 0); // 9pm local
            Assert.IsTrue(rules.IsNight(local));
            Assert.IsFalse(rules.IsNight(new DateTime(2026, 9, 28, 15, 0, 0)));
            s.onlineReadyAt = 0; // one free steamer waiting at bedtime
            int before = s.steamers;
            rules.GoToSleep(utc, local);
            Assert.IsTrue(s.asleep);
            Assert.AreEqual(c.rules.nightDrain, rules.DrainScaleAt(utc.AddHours(5).Ticks), 1e-6, "slow drain in the night");
            Assert.AreEqual(1f, rules.DrainScaleAt(utc.AddHours(14).Ticks), 1e-6, "normal again after the wake-by hour (10am)");
            clock.Advance(TimeSpan.FromHours(10)); // 7am
            var r = rules.WakeUp(clock.UtcNow);
            Assert.IsFalse(s.asleep);
            int night = GameRules.ResetsBetween(utc.Ticks, utc.AddHours(10).Ticks, c.rules.giftHours); // one per reset in the night
            Assert.AreEqual(1 + night, s.steamers - before, "the free one waiting at bedtime (handed over then) plus the night's");
            Assert.AreEqual(night, r.steamers, "the wake-up screen counts only the night's own");
        }

        [Test]
        public void Missions_Need_What_You_Own_And_Dont_Repeat_Straight_Away()
        {
            var c = Content();
            var s = GameRules.NewState(c, 5);
            var rules = new GameRules(c, s);
            s.items.RemoveAll(p => p.Arch == "slide" || p.Arch == "trampoline");
            s.friends.Clear();
            var seen = new System.Collections.Generic.List<string>();
            for (int n = 0; n < 60; n++)
            {
                var t = rules.NewTask();
                var d = rules.TaskDef(t);
                Assert.IsTrue(rules.CanDo(d), d.id + " needs something the room doesn't have");
                Assert.AreNotEqual("slide", d.id);
                Assert.AreNotEqual("visit", d.id);
                int last = seen.LastIndexOf(t.id);
                if (last >= 0) Assert.GreaterOrEqual(seen.Count - last, c.rules.taskMemory, t.id + " came back too soon");
                seen.Add(t.id);
            }
        }

        [Test]
        public void Friend_Visits_Carry_Their_Care_Over_Once()
        {
            var c = Content();
            var s = GameRules.NewState(c, 44);
            var rules = new GameRules(c, s);
            s.needs[Needs.Hunger] = .3f;
            s.needs[Needs.Play] = .3f;
            int coins = s.coins;
            var g = rules.CreditVisit("mia", 1000, 3, "pet,feed,water", "Mia");
            Assert.IsNotNull(g);
            Assert.AreEqual("Mia", g.name);
            Assert.AreEqual(.3f + c.rules.visitFeed, s.needs[Needs.Hunger], 1e-4f);
            Assert.AreEqual(.3f + c.rules.visitPet, s.needs[Needs.Play], 1e-4f);
            Assert.IsTrue(g.watered);
            Assert.Greater(s.coins, coins);
            Assert.IsNull(rules.CreditVisit("mia", 1000, 3, "pet,feed,water", "Mia"), "the same visit only counts once");
        }

        private sealed class MemoryStore : ISaveStore
        {
            public string Text;
            public bool TryRead(out string text) { text = Text; return Text != null; }
            public void Write(string text) { Text = text; }
            public void WriteCorruptCopy(string text) { }
        }

        [Test]
        public void Save_Round_Trips_And_Old_Saves_Start_Fresh()
        {
            var c = Content();
            var store = new MemoryStore();
            var service = new SaveService(store, new JsonUtilitySaveSerializer(false), SaveMigrator.CreateDefault(), new ManualClock(new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc)));
            LoadOutcome outcome;
            var data = service.Load(t => GameRules.NewState(c, (ulong)t), out outcome);
            Assert.AreEqual(LoadOutcome.NewGame, outcome);
            data.state.coins = 999;
            service.Save(data);
            var again = service.Load(t => GameRules.NewState(c, (ulong)t), out outcome);
            Assert.AreEqual(LoadOutcome.Loaded, outcome);
            Assert.AreEqual(999, again.state.coins);
            Assert.AreEqual(12, again.state.items.Count);
            store.Text = "{\"version\":2,\"coins\":5}";
            service.Load(t => GameRules.NewState(c, (ulong)t), out outcome);
            Assert.AreEqual(LoadOutcome.NewGame, outcome);
        }
    }
}
