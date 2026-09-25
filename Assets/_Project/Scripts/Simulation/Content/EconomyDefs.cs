using System;

namespace Squishy.Simulation.Content
{
    [Serializable]
    public class TaskDef
    {
        public string id;
        public string description;
        /// <summary>
        /// Game event that advances this task, e.g. "cook.recipe", "squish", "plant.water".
        /// New tasks built on existing events need no code.
        /// </summary>
        public string eventId;
        public int targetCount = 1;
        public int coinReward;
    }

    [Serializable]
    public class EconomyDef
    {
        public int startingCoins = 248;
        public int startingSteamers = 3;
        public int startingRoomLevel = 2;
        /// <summary>Finish id of the squishy a new player starts with (one copy, as favourite).</summary>
        public string startingSquishyId = "peach";

        public int steamerPrice = 150;
        public int tasksActive = 3;
        public int tasksPerSteamerReward = 3;

        public float happyIncomePerMinute = 2f;
        /// <summary>Income multiplier is 1 + Comfort / this.</summary>
        public float comfortIncomeDivisor = 20f;
        /// <summary>Drain slowdown per Comfort point (0.02 = 2%).</summary>
        public float comfortDrainPerPoint = 0.02f;
        public float maxComfortDrainReduction = 0.4f;
        public int styleSetSize = 3;
        public int styleSetComfortBonus = 3;

        public int duplicateFurnitureSkinCoins = 20;
        public int duplicateToolCoins = 10;
        public int duplicateToolSkinCoins = 15;
        public int duplicateSteamerSkinCoins = 30;

        /// <summary>Repair cost is this fraction of the tool's price, scaled by wear.</summary>
        public float repairPriceFraction = 0.5f;
    }
}
