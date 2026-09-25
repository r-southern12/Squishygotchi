using System.Collections.Generic;
using NUnit.Framework;
using Squishy.Data;
using Squishy.Simulation.Content;
using Squishy.Simulation.Core;
using Squishy.Simulation.Gacha;
using UnityEditor;

namespace Squishy.Tests
{
    public class GachaOddsTests
    {
        private const int Opens = 10000;
        private const ulong Seed = 20260925UL;

        // The published odds from docs/spec.md. Tests check the data against these.
        private static readonly Dictionary<Rarity, double> Published = new Dictionary<Rarity, double>
        {
            { Rarity.Common, 0.76 }, { Rarity.Rare, 0.18 }, { Rarity.Epic, 0.05 }, { Rarity.Legendary, 0.01 },
        };

        private static DropTableDef SpecTable(bool withPity)
        {
            var t = new DropTableDef { favouriteCopyChance = 0.4f };
            t.rarities.Add(new RarityWeight { rarity = Rarity.Common, weight = 76 });
            t.rarities.Add(new RarityWeight { rarity = Rarity.Rare, weight = 18 });
            t.rarities.Add(new RarityWeight { rarity = Rarity.Epic, weight = 5 });
            t.rarities.Add(new RarityWeight { rarity = Rarity.Legendary, weight = 1 });
            if (withPity)
            {
                t.pity.Add(new PityRule { minRarity = Rarity.Rare, guaranteedWithin = 10 });
                t.pity.Add(new PityRule { minRarity = Rarity.Epic, guaranteedWithin = 50 });
            }
            foreach (Rarity r in System.Enum.GetValues(typeof(Rarity)))
            {
                var ct = new CategoryTable { rarity = r };
                ct.categories.Add(new CategoryWeight { category = RewardCategory.FurnitureSkin, weight = 1 });
                ct.categories.Add(new CategoryWeight { category = RewardCategory.Squishy, weight = 1 });
                t.categoryTables.Add(ct);
            }
            return t;
        }

        private static Dictionary<Rarity, int> Simulate(DropTableDef table, ulong seed, int opens,
            out int longestWithoutRare, out int longestWithoutEpic)
        {
            var roller = new GachaRoller(table, new Pcg32(seed));
            var pity = new PityState();
            var counts = new Dictionary<Rarity, int>();
            foreach (Rarity r in System.Enum.GetValues(typeof(Rarity))) counts[r] = 0;

            int sinceRare = 0, sinceEpic = 0;
            longestWithoutRare = 0;
            longestWithoutEpic = 0;
            for (int i = 0; i < opens; i++)
            {
                var result = roller.Open(pity);
                counts[result.rarity]++;
                // Streak length counts the open that finally hit, so "within 10" means <= 10.
                sinceRare++;
                sinceEpic++;
                if (result.rarity >= Rarity.Rare) { if (sinceRare > longestWithoutRare) longestWithoutRare = sinceRare; sinceRare = 0; }
                if (result.rarity >= Rarity.Epic) { if (sinceEpic > longestWithoutEpic) longestWithoutEpic = sinceEpic; sinceEpic = 0; }
            }
            return counts;
        }

        [Test]
        public void BaseOdds_Over10000Opens_MatchPublishedRates()
        {
            int a, b;
            var counts = Simulate(SpecTable(false), Seed, Opens, out a, out b);

            // Tolerances are about 4 standard deviations for 10,000 opens.
            AssertRate(counts, Rarity.Common, 0.76, 0.018);
            AssertRate(counts, Rarity.Rare, 0.18, 0.016);
            AssertRate(counts, Rarity.Epic, 0.05, 0.009);
            AssertRate(counts, Rarity.Legendary, 0.01, 0.004);
        }

        [Test]
        public void Pity_Over10000Opens_GuaranteesRareIn10AndEpicIn50()
        {
            int longestWithoutRare, longestWithoutEpic;
            Simulate(SpecTable(true), Seed, Opens, out longestWithoutRare, out longestWithoutEpic);

            Assert.LessOrEqual(longestWithoutRare, 10, "Rare or better must arrive within 10 opens.");
            Assert.LessOrEqual(longestWithoutEpic, 50, "Epic or better must arrive within 50 opens.");
        }

        [Test]
        public void Pity_Over10000Opens_OnlyNudgesRatesUpward()
        {
            int a, b;
            var counts = Simulate(SpecTable(true), Seed, Opens, out a, out b);

            // Pity turns some Commons into Rares/Epics. The prototype measured 74 / 19 / 5.6 / 1.
            AssertRange(counts, Rarity.Common, 0.72, 0.77);
            AssertRange(counts, Rarity.Rare, 0.17, 0.21);
            AssertRange(counts, Rarity.Epic, 0.045, 0.068);
            AssertRange(counts, Rarity.Legendary, 0.005, 0.015);
        }

        [Test]
        public void Pity_TenthOpenAfterNineCommons_IsRarePlus()
        {
            // A table that only ever rolls Common, so pity is the only way to get Rare.
            var t = SpecTable(true);
            t.rarities.RemoveAll(r => r.rarity != Rarity.Common);
            var roller = new GachaRoller(t, new Pcg32(1));
            var pity = new PityState();

            for (int i = 0; i < 9; i++) Assert.AreEqual(Rarity.Common, roller.Open(pity).rarity, "open " + (i + 1));
            var tenth = roller.Open(pity);
            Assert.AreEqual(Rarity.Rare, tenth.rarity);
            Assert.IsTrue(tenth.pityTriggered);
            Assert.AreEqual(10, roller.OpensUntilGuarantee(0, pity), "Counter resets after a Rare.");
        }

        [Test]
        public void SameSeedAndPity_GiveSameResults()
        {
            var table = SpecTable(true);
            var first = new GachaRoller(table, new Pcg32(42));
            var second = new GachaRoller(table, new Pcg32(42));
            var pityA = new PityState();
            var pityB = new PityState();
            for (int i = 0; i < 500; i++)
            {
                var x = first.Open(pityA);
                var y = second.Open(pityB);
                Assert.AreEqual(x.rarity, y.rarity);
                Assert.AreEqual(x.category, y.category);
                Assert.AreEqual(x.isFavouriteCopy, y.isFavouriteCopy);
            }
        }

        [Test]
        public void RngResumedFromSavedState_ContinuesSameSequence()
        {
            var rng = new Pcg32(7);
            for (int i = 0; i < 100; i++) rng.NextUInt();
            var resumed = Pcg32.FromState(rng.State);
            for (int i = 0; i < 100; i++) Assert.AreEqual(rng.NextUInt(), resumed.NextUInt());
        }

        [Test]
        public void ShippedDropTable_PublishesSpecOddsAndPity()
        {
            string[] guids = AssetDatabase.FindAssets("t:DropTableAsset", new[] { "Assets/_Project/Content" });
            if (guids.Length == 0) Assert.Ignore("No DropTable asset yet. Run Squishy > Content > Seed From Spec.");
            var asset = AssetDatabase.LoadAssetAtPath<DropTableAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var table = asset.def;

            CollectionAssert.IsEmpty(table.Validate());
            foreach (var kv in Published)
                Assert.AreEqual(kv.Value, table.PublishedChance(kv.Key), 0.0001, kv.Key + " published chance");

            int longestWithoutRare, longestWithoutEpic;
            var counts = Simulate(table, Seed, Opens, out longestWithoutRare, out longestWithoutEpic);
            Assert.LessOrEqual(longestWithoutRare, 10);
            Assert.LessOrEqual(longestWithoutEpic, 50);
            AssertRange(counts, Rarity.Legendary, 0.005, 0.015);
        }

        private static void AssertRate(Dictionary<Rarity, int> counts, Rarity rarity, double expected, double tolerance)
        {
            double rate = counts[rarity] / (double)Opens;
            Assert.AreEqual(expected, rate, tolerance, rarity + " rate was " + rate.ToString("P2"));
        }

        private static void AssertRange(Dictionary<Rarity, int> counts, Rarity rarity, double min, double max)
        {
            double rate = counts[rarity] / (double)Opens;
            Assert.That(rate, Is.InRange(min, max), rarity + " rate was " + rate.ToString("P2"));
        }
    }
}
