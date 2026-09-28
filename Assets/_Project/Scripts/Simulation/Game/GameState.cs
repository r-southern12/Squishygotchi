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
        public int tipPile; // coins waiting in the spinning coin over its head (tap to collect)
        public long tipStart; // when the first of them arrived
        public bool dead;
        /// <summary>Tucked in: needs, ageing and the death clock are paused (holidays, busy weeks).</summary>
        public bool tucked;
        public int sinceRare, sinceEpic, sinceLegendary;
        public ulong rng;
        public List<TaskState> tasks = new List<TaskState>();
        public List<string> recentTasks = new List<string>(); // the last few tasks shown, so they don't come straight back
        public List<PieceState> items = new List<PieceState>();
        public List<string> storage = new List<string>();

        // Life cycle and meta progression.
        public float qolSum, qolTime;
        public List<LifeState> lifeOf = new List<LifeState>(); // each squishy type's own life while it isn't the favourite
        public int prestige;
        public List<LifeRecord> lives = new List<LifeRecord>();
        public List<string> cosmetics = new List<string>();
        public string hat = "", face = "", neck = "";
        public bool premium;
        public long onlineReadyAt; // when the next free in-game steamer can be claimed
        public bool asleep; // turned in for the night
        public long sleepAt, sleepUntil, lastNightPrompt, nightSnoozeUntil;
        public int sleepSteamers, nightBank;
        public long bonusReadyAt; // when the next bonus (video) steamer is available
        public long trialStart, lastTaskDay, weekStart, lastSeenUtc;
        public int streak, weekTasks;
        public int stageAwarded; // highest life stage whose prestige has been paid this life
        public bool weekClaimed;
        public List<string> tiersClaimed = new List<string>();
        public bool soundOn = true, musicOn = true, hapticsOn = true, notificationsOn = true;
        public string track = ""; // chosen music ("" follows the steamer in use)
        public List<SoundPick> soundPicks = new List<SoundPick>(); // Sound Lab choices (clip per sound event)

        // Player profile: kept on this phone; the friend code is what friends will use to visit.
        public string playerName = "", avatar = "", friendCode = "";
        public List<FriendData> friends = new List<FriendData>();
        public List<VisitCredit> visitsCredited = new List<VisitCredit>();
        public bool welcomed;
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
        public float hx, hz; // a ball's home: where it was last placed (it pops back there after play)
        public bool homeSet;

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

namespace Squishy.Simulation.Game
{
    /// <summary>A friend you've visited: their cloud player id, code and what their squishy looked like last time.</summary>
    [System.Serializable]
    public class FriendData { public string code, id, avatar; public int finish; }

    /// <summary>The latest visit from one friend that has already paid you coins.</summary>
    [System.Serializable]
    public class VisitCredit { public string id; public long at; }

    /// <summary>A Sound Lab choice: which clip (and how loud) plays for a sound event.</summary>
    [System.Serializable]
    public class SoundPick { public string ev, clip; public float vol = -1; }
}

namespace Squishy.Simulation.Game
{
    /// <summary>A squishy type's own life, kept while another squishy is the favourite.</summary>
    [System.Serializable]
    public class LifeState { public int i, age, stageAwarded; public float dayT, qolSum, qolTime; }
}
