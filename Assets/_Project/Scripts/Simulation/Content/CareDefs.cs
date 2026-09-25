using System;

namespace Squishy.Simulation.Content
{
    /// <summary>
    /// Tuning for needs, self-care, decline and death. Defaults are the prototype's test-speed
    /// values (a full need empties in 20-40 minutes); the release target is 8-16 hours.
    /// </summary>
    [Serializable]
    public class CareDef
    {
        public float hungerDrainPerSecond = 0.0007f;
        public float playDrainPerSecond = 0.0009f;
        public float restDrainPerSecond = 0.0005f;
        public float cleanDrainPerSecond = 0.0004f;

        /// <summary>The squishy looks after a need on its own once it drops below this.</summary>
        public float selfCareBelow = 0.35f;
        /// <summary>The squishy on its own never lifts a need above this.</summary>
        public float selfCareCap = 0.5f;
        /// <summary>Chance per idle decision that it looks after its lowest need (when below selfCareBelow).</summary>
        public float selfCareChance = 0.8f;
        /// <summary>"Make do" self-care when no station suits: fill rate and duration.</summary>
        public float makeDoRate = 0.06f;
        public float makeDoSeconds = 4f;

        public float happyAbove = 0.5f;
        public float droopyAbove = 0.25f;
        /// <summary>Below this the squishy is Critical: grey, and too weak to look after itself.</summary>
        public float criticalBelow = 0.1f;
        /// <summary>Idle "Hungry!"-style warnings when the lowest need is under this.</summary>
        public float warnBelow = 0.3f;

        /// <summary>Seconds a need can sit at zero before death. Prototype 300; release target about 24 hours.</summary>
        public float deathSecondsAtZero = 300f;
        public float dayLengthSeconds = 600f;

        /// <summary>Offline catch-up is simulated in steps of this size.</summary>
        public float offlineStepSeconds = 30f;
        /// <summary>Average self-care fill rate per second while the app is closed (it self-cares in bursts online).</summary>
        public float offlineSelfCareRate = 0.03f;
        public float maxOfflineSeconds = 7f * 24f * 3600f;

        public float DrainPerSecond(NeedKind need)
        {
            switch (need)
            {
                case NeedKind.Hunger: return hungerDrainPerSecond;
                case NeedKind.Play: return playDrainPerSecond;
                case NeedKind.Rest: return restDrainPerSecond;
                default: return cleanDrainPerSecond;
            }
        }
    }

    /// <summary>Something the squishy does at a station (or on the spot) that fills needs over time.</summary>
    [Serializable]
    public class ActivityDef
    {
        public string id;
        public string label;
        public bool fillsNeed = true;
        public NeedKind need;
        /// <summary>A second need filled at half rate (tea time also fills Hunger).</summary>
        public bool fillsAlso;
        public NeedKind alsoNeed;
        public float seconds = 4f;
        public float ratePerSecond = 0.1f;
        /// <summary>Cap when the player sends the squishy (1 = can fill completely).</summary>
        public float playerCap = 1f;
        /// <summary>Whether the squishy picks this on its own (still capped by CareDef.selfCareCap).</summary>
        public bool selfCare;
        /// <summary>Only works with a seat (stool or cushion) near the item.</summary>
        public bool needsSeatNearby;
        /// <summary>Napping: a lamp left on halves the fill for the player.</summary>
        public bool isSleep;
        /// <summary>Uses one snack from the pantry.</summary>
        public bool usesSnack;
    }

    /// <summary>Which activity an item type offers.</summary>
    [Serializable]
    public class ItemActivityDef
    {
        public string itemTypeId;
        public string activityId;
    }

    /// <summary>A piece in the starting room. Positions are metres from the room centre at level 2.</summary>
    [Serializable]
    public class StarterPieceDef
    {
        public string itemTypeId;
        public string styleId;
        public float x;
        public float z;
        public float yawDegrees;
    }
}
