using System;
using System.Collections.Generic;

namespace Squishy.Simulation.Game
{
    /// <summary>Everything that persists: wallet, collection, kitchen, needs, pity, tasks and the room layout.</summary>
    [Serializable]
    public class GameState
    {
        public int coins, steamers, favIdx, roomLv, age, curSkin;
        public List<CountData> squishOwned = new List<CountData>();
        public List<string> owned = new List<string>();
        public int[] pantry, snacks, toolDur, toolSkin, recipeXP;
        public float[] needs = new float[4]; // hunger, play, rest, clean
        public float dayT, deathClock, happyT;
        public bool dead;
        /// <summary>Tucked in: needs, ageing and the death clock are paused (holidays, busy weeks).</summary>
        public bool tucked;
        public int sinceRare, sinceEpic, setDone;
        public ulong rng;
        public List<TaskState> tasks = new List<TaskState>();
        public List<PieceState> items = new List<PieceState>();
        public List<string> storage = new List<string>();

        // Life cycle and meta progression.
        public float qolSum, qolTime;
        public int prestige;
        public List<LifeRecord> lives = new List<LifeRecord>();
        public List<string> cosmetics = new List<string>();
        public string hat = "", face = "", neck = "";
        public bool premium;
        public long trialStart, giftReadyAt, lastTaskDay, weekStart, lastSeenUtc;
        public int streak, weekTasks;
        public bool weekClaimed;
        public List<string> tiersClaimed = new List<string>();
        public bool soundOn = false, hapticsOn = true, notificationsOn = true;
    }

    [Serializable]
    public class TaskState
    {
        public string id;
        public float prog;
        public bool done;
        /// <summary>UTC ticks when this slot gets a task again after a claim (0 = active now).</summary>
        public long readyAt;
    }

    /// <summary>A piece placed in the room. Runtime objects keep a reference and write back as they move.</summary>
    [Serializable]
    public class PieceState
    {
        public string key;
        public float x, z, ry, wilt;
        public bool lampOn = true;

        public string Arch { get { int i = key.IndexOf(':'); return i < 0 ? key : key.Substring(0, i); } }
        public string Style { get { int i = key.IndexOf(':'); return i < 0 ? "" : key.Substring(i + 1); } }
    }

    public static class Needs
    {
        public const int Hunger = 0, Play = 1, Rest = 2, Clean = 3;
        public static readonly string[] Names = { "hunger", "play", "rest", "clean" };

        public static int Index(string name) { return Array.IndexOf(Names, name); }
    }
}
