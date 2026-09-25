using System;
using NUnit.Framework;
using Squishy.Runtime.Save;
using Squishy.Simulation.Content;
using Squishy.Simulation.Core;
using Squishy.Simulation.Save;

namespace Squishy.Tests
{
    public class SaveTests
    {
        private sealed class MemoryStore : ISaveStore
        {
            public string Text;

            public bool TryRead(out string text)
            {
                text = Text;
                return Text != null;
            }

            public void Write(string text)
            {
                Text = text;
            }
        }

        private sealed class AddCoinsMigration : ISaveMigration
        {
            private readonly int _from;
            public AddCoinsMigration(int from) { _from = from; }
            public int FromVersion { get { return _from; } }
            public void Apply(SaveData data) { data.coins += 1; }
        }

        private MemoryStore _store;
        private ManualClock _clock;
        private SaveService _service;
        private EconomyDef _economy;

        [SetUp]
        public void SetUp()
        {
            _store = new MemoryStore();
            _clock = new ManualClock(new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc));
            _service = new SaveService(_store, new JsonUtilitySaveSerializer(false), SaveMigrator.CreateDefault(), _clock);
            _economy = new EconomyDef { startingCoins = 248, startingSteamers = 3, startingRoomLevel = 2 };
        }

        [Test]
        public void NoSave_StartsNewGameFromEconomyData()
        {
            LoadOutcome outcome;
            var data = _service.Load(_economy, out outcome);

            Assert.AreEqual(LoadOutcome.NewGame, outcome);
            Assert.AreEqual(248, data.coins);
            Assert.AreEqual(3, data.steamers);
            Assert.AreEqual(2, data.roomLevel);
            Assert.AreEqual(SaveMigrator.CurrentVersion, data.version);
        }

        [Test]
        public void SaveThenLoad_KeepsEverything()
        {
            LoadOutcome outcome;
            var data = _service.Load(_economy, out outcome);
            data.coins = 999;
            data.gachaRngState = 0xFEDCBA9876543210UL; // above long.MaxValue, checks ulong survives JSON
            data.pity.sinceGuarantee.Add(7);
            data.pity.sinceGuarantee.Add(33);
            data.favouriteSquishyId = "peach";
            data.squishies.Add(new OwnedCount("peach", 4));
            data.unlockedSkinIds.Add("bed:riad");

            _clock.Advance(TimeSpan.FromHours(3));
            _service.Save(data);
            var loaded = _service.Load(_economy, out outcome);

            Assert.AreEqual(LoadOutcome.Loaded, outcome);
            Assert.AreEqual(999, loaded.coins);
            Assert.AreEqual(0xFEDCBA9876543210UL, loaded.gachaRngState);
            CollectionAssert.AreEqual(new[] { 7, 33 }, loaded.pity.sinceGuarantee);
            Assert.AreEqual("peach", loaded.favouriteSquishyId);
            Assert.AreEqual(4, loaded.squishies[0].count);
            CollectionAssert.AreEqual(new[] { "bed:riad" }, loaded.unlockedSkinIds);
            Assert.AreEqual(_clock.UtcNow, loaded.LastSavedUtc);
        }

        [Test]
        public void UnreadableSave_StartsNewGameAndKeepsOldText()
        {
            _store.Text = "{ this is not json";
            LoadOutcome outcome;
            var data = _service.Load(_economy, out outcome);

            Assert.AreEqual(LoadOutcome.Corrupt, outcome);
            Assert.AreEqual(248, data.coins);
            Assert.AreEqual("{ this is not json", _service.LastCorruptText);
        }

        [Test]
        public void Migrator_RunsEachStepInOrder()
        {
            var migrator = new SaveMigrator(new ISaveMigration[] { new AddCoinsMigration(2), new AddCoinsMigration(1) });
            var data = new SaveData { version = 1, coins = 0 };

            migrator.Migrate(data, 3);

            Assert.AreEqual(3, data.version);
            Assert.AreEqual(2, data.coins);
        }

        [Test]
        public void Migrator_MissingStep_Throws()
        {
            var migrator = new SaveMigrator(new ISaveMigration[] { new AddCoinsMigration(1) });
            Assert.Throws<SaveVersionException>(() => migrator.Migrate(new SaveData { version = 1 }, 3));
        }

        [Test]
        public void SaveFromNewerBuild_IsRefusedNotOverwritten()
        {
            var future = new SaveData { version = SaveMigrator.CurrentVersion + 1, coins = 5 };
            _store.Text = new JsonUtilitySaveSerializer(false).Serialize(future);
            string before = _store.Text;

            LoadOutcome outcome;
            Assert.Throws<SaveVersionException>(() => _service.Load(_economy, out outcome));
            Assert.AreEqual(before, _store.Text);
        }
    }
}
