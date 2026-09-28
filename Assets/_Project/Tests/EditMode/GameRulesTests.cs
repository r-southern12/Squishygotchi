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
            int a = s.favIdx, b = s.squishOwned.Find(q => q.i != a).i;
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
            var clock = new ManualClock(new DateTime(2026, 9, 25, 9, 0, 0, DateTimeKind.Utc));
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
            Assert.AreEqual(steamers + 3 * c.rules.taskSteamers + c.rules.taskSetSteamers, s.steamers, "a steamer per task, plus the set bonus");
        }

        [Test]
        public void Recipes_Need_Ingredients_Tools_And_Kitchen_Level()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 1));
            Assert.IsEmpty(rules.MissingFor(c.recipes[0]), "congee is always free");
            Assert.IsEmpty(rules.MissingFor(c.recipes[2]), "pork & chive stir-fry: pork, chives, wok (all in the starter kitchen)");
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
            CollectionAssert.AreEqual(new[] { 0, 1 }, rules.UnlockedTracks(), "the starter steamers' tracks at first");
            rules.AddOwned("skin:3");
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, rules.UnlockedTracks());
        }

        [Test]
        public void Steamers_Arrive_Every_Few_Hours_Plus_Online_And_Bonus()
        {
            var c = Content();
            var s = GameRules.NewState(c, 9);
            var clock = new ManualClock(new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc));
            var rules = new GameRules(c, s) { Clock = clock };
            s.giftReadyAt = 0;
            Assert.AreEqual(0, rules.AccrueGifts(), "the first one comes after the wait");
            int before = s.steamers;
            clock.Advance(TimeSpan.FromHours(c.rules.giftHours * 2 + .5));
            Assert.AreEqual(2, rules.AccrueGifts(), "two waits, two steamers, added on their own");
            Assert.AreEqual(before + 2, s.steamers);
            clock.Advance(TimeSpan.FromDays(30));
            Assert.AreEqual(c.rules.giftStackMax, rules.AccrueGifts(), "a long time away is capped");
            Assert.AreEqual(0, rules.AccrueGifts());
            // In the game: one more free, and one more for an optional video, each once per wait.
            Assert.IsTrue(rules.ClaimOnline());
            Assert.IsFalse(rules.ClaimOnline());
            Assert.IsTrue(rules.ClaimBonus());
            Assert.IsFalse(rules.ClaimBonus());
            clock.Advance(TimeSpan.FromHours(c.rules.giftHours));
            Assert.IsTrue(rules.ClaimOnline() && rules.ClaimBonus());
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
            rules.AccrueGifts(); // the arrivals timer is already running in the game
            s.onlineReadyAt = 0; // one free steamer waiting at bedtime
            int before = s.steamers;
            rules.GoToSleep(utc, local);
            Assert.IsTrue(s.asleep);
            Assert.AreEqual(c.rules.nightDrain, rules.DrainScaleAt(utc.AddHours(5).Ticks), 1e-6, "slow drain in the night");
            Assert.AreEqual(1f, rules.DrainScaleAt(utc.AddHours(14).Ticks), 1e-6, "normal again after the wake-by hour (10am)");
            clock.Advance(TimeSpan.FromHours(10)); // 7am
            var r = rules.WakeUp(clock.UtcNow);
            Assert.IsFalse(s.asleep);
            int online = 1 + (int)(10 / c.rules.giftHours);
            int passive = (int)(10 / c.rules.giftHours); // arrivals on their own (the first came due during the night)
            Assert.GreaterOrEqual(s.steamers - before, online + passive - 1);
            Assert.AreEqual(s.steamers - before, r.steamers);
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
