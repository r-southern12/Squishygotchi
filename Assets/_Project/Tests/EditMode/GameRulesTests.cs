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
                rules.TaskEvent(t.id, d.goal);
                paid += d.coins;
                rules.ClaimTask(0);
                Assert.IsFalse(rules.TaskReady(s.tasks[0]), "a claimed slot rests before its next task");
                clock.Advance(TimeSpan.FromHours(c.rules.taskCooldownHours));
            }
            Assert.AreEqual(coins + paid, s.coins);
            Assert.AreEqual(steamers + c.rules.taskSetSteamers, s.steamers);
        }

        [Test]
        public void Recipes_Need_Ingredients_Tools_And_Kitchen_Level()
        {
            var c = Content();
            var rules = new GameRules(c, GameRules.NewState(c, 1));
            Assert.IsEmpty(rules.MissingFor(c.recipes[0]), "congee is always free");
            Assert.IsEmpty(rules.MissingFor(c.recipes[1]), "scallion buns: flour, scallion, mini steamer");
            CollectionAssert.Contains(rules.MissingFor(c.recipes[8]), "Kitchen Lv 4");
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
