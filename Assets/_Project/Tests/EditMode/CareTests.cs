using System.Collections.Generic;
using NUnit.Framework;
using Squishy.Simulation.Care;
using Squishy.Simulation.Content;
using Squishy.Simulation.Room;
using Squishy.Simulation.Save;

namespace Squishy.Tests
{
    public class CareTests
    {
        private CareDef _def;
        private EconomyDef _economy;

        [SetUp]
        public void SetUp()
        {
            _def = new CareDef();
            _economy = new EconomyDef();
        }

        [Test]
        public void Tick_DrainsEachNeedAtItsRate_SlowedByComfort()
        {
            var s = new CareState();
            CareSim.Tick(s, _def, 100f, 0.25f);
            Assert.AreEqual(1f - _def.hungerDrainPerSecond * 100f * 0.75f, s.hunger, 1e-5);
            Assert.AreEqual(1f - _def.playDrainPerSecond * 100f * 0.75f, s.play, 1e-5);
            Assert.AreEqual(100f, s.ageSeconds, 1e-3);
        }

        [Test]
        public void ComfortSlowdown_Is2PercentPerPoint_CappedAt40()
        {
            Assert.AreEqual(0.1f, CareSim.ComfortSlowdown(5, _economy), 1e-5);
            Assert.AreEqual(0.4f, CareSim.ComfortSlowdown(50, _economy), 1e-5);
            Assert.AreEqual(0f, CareSim.ComfortSlowdown(-3, _economy), 1e-5);
        }

        [TestCase(0.8f, CareStage.Happy)]
        [TestCase(0.4f, CareStage.Droopy)]
        [TestCase(0.2f, CareStage.Flat)]
        [TestCase(0.05f, CareStage.Critical)]
        public void Stage_FollowsLowestNeed(float lowest, CareStage expected)
        {
            var s = new CareState { rest = lowest };
            Assert.AreEqual(expected, CareSim.Stage(s, _def));
        }

        [Test]
        public void SelfCare_NeverFillsAboveAutonomyCap_PlayerCanFillFully()
        {
            var nap = new ActivityDef { need = NeedKind.Rest, playerCap = 1f, selfCare = true };
            var s = new CareState { rest = 0.3f };

            CareSim.Fill(s, NeedKind.Rest, 5f, CareSim.CapFor(nap, false, _def));
            Assert.AreEqual(0.5f, s.rest, 1e-5, "On its own it stops at about half.");

            CareSim.Fill(s, NeedKind.Rest, 5f, CareSim.CapFor(nap, true, _def));
            Assert.AreEqual(1f, s.rest, 1e-5, "Only the player can fill it completely.");
        }

        [Test]
        public void Fill_NeverLowersANeedAboveTheCap()
        {
            var s = new CareState { hunger = 0.9f };
            CareSim.Fill(s, NeedKind.Hunger, 0.5f, 0.6f); // a snack when already full
            Assert.AreEqual(0.9f, s.hunger, 1e-5);
        }

        [Test]
        public void Critical_CannotSelfCare()
        {
            Assert.IsTrue(CareSim.CanSelfCare(new CareState { clean = 0.2f }, _def));
            Assert.IsFalse(CareSim.CanSelfCare(new CareState { clean = 0.05f }, _def));
        }

        [Test]
        public void Death_AfterNeedSitsAtZeroTooLong_ClockWindsBackWhenFed()
        {
            var s = new CareState { hunger = 0f };
            Assert.IsFalse(CareSim.Tick(s, _def, _def.deathSecondsAtZero * 0.8f, 0f));
            s.hunger = 1f;
            CareSim.Tick(s, _def, _def.deathSecondsAtZero * 0.5f, 0f);
            Assert.Less(s.secondsAtZero, _def.deathSecondsAtZero * 0.8f, "Clock winds down once no need is empty.");

            s.hunger = 0f;
            bool died = false;
            for (int i = 0; i < 100 && !died; i++) died = CareSim.Tick(s, _def, 10f, 0f);
            Assert.IsTrue(died);
            Assert.IsTrue(s.dead);
            Assert.AreEqual(NeedKind.Hunger, s.causeOfDeath);
            Assert.AreEqual(CareStage.Dead, CareSim.Stage(s, _def));
        }

        [Test]
        public void Offline_HealthySquishyScrapesByButStaysUnderHalf()
        {
            var s = new CareState();
            bool died = CareSim.SimulateOffline(s, _def, 6 * 3600, 0f);
            Assert.IsFalse(died);
            Assert.Greater(CareSim.Condition(s), _def.criticalBelow);
            Assert.LessOrEqual(CareSim.Condition(s), _def.selfCareCap + 1e-4f);
        }

        [Test]
        public void Offline_CriticalSquishyCantHelpItselfAndDies()
        {
            var s = new CareState { hunger = 0.02f, play = 0.3f, rest = 0.3f, clean = 0.3f };
            bool died = CareSim.SimulateOffline(s, _def, 3600, 0f);
            Assert.IsTrue(died);
            Assert.AreEqual(NeedKind.Hunger, s.causeOfDeath);
        }

        [Test]
        public void NextGeneration_StartsFreshAndCountsUp()
        {
            var s = new CareState { hunger = 0f, dead = true, generation = 2, ageSeconds = 999f, secondsAtZero = 400f };
            CareSim.StartNextGeneration(s);
            Assert.IsFalse(s.dead);
            Assert.AreEqual(3, s.generation);
            Assert.AreEqual(1f, CareSim.Condition(s), 1e-5);
            Assert.AreEqual(0f, s.ageSeconds, 1e-5);
        }

        [Test]
        public void Comfort_SumsPlacedPieces_PlusOneSetBonus()
        {
            var types = new Dictionary<string, ItemTypeDef>
            {
                { "bed", new ItemTypeDef { id = "bed", comfort = 2 } },
                { "lamp", new ItemTypeDef { id = "lamp", comfort = 2 } },
                { "rug", new ItemTypeDef { id = "rug", comfort = 1 } },
            };
            var pieces = new List<PlacedPiece>
            {
                Piece("bed", "riad"), Piece("lamp", "riad"), Piece("rug", "riad"), Piece("lamp", "nordic"),
                new PlacedPiece { itemTypeId = "bed", skinId = "bed:riad", inStorage = true },
            };
            int comfort = RoomRules.Comfort(pieces, id => types[id], _economy);
            Assert.AreEqual(2 + 2 + 1 + 2 + 3, comfort, "Storage doesn't count; three Riad pieces give +3.");
        }

        [Test]
        public void StarterRoom_OnlyFillsAnEmptyRoom()
        {
            var starter = new List<StarterPieceDef> { new StarterPieceDef { itemTypeId = "bed", styleId = "cottage", x = 1f } };
            var pieces = new List<PlacedPiece>();
            Assert.IsTrue(RoomRules.EnsureStarterRoom(pieces, starter));
            Assert.AreEqual("bed:cottage", pieces[0].skinId);
            Assert.AreEqual("cottage", pieces[0].StyleId);
            Assert.IsFalse(RoomRules.EnsureStarterRoom(pieces, starter));
            Assert.AreEqual(1, pieces.Count);
        }

        [Test]
        public void Migration_V1SaveGainsCareAndRoom()
        {
            var old = new SaveData { version = 1, care = null, pieces = null };
            SaveMigrator.CreateDefault().Migrate(old);
            Assert.AreEqual(SaveMigrator.CurrentVersion, old.version);
            Assert.IsNotNull(old.care);
            Assert.IsNotNull(old.pieces);
            Assert.AreEqual(1f, old.care.hunger, 1e-5);
        }

        private static PlacedPiece Piece(string type, string style)
        {
            return new PlacedPiece { itemTypeId = type, skinId = PlacedPiece.SkinIdFor(type, style) };
        }
    }
}
