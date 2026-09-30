using System;

namespace Squishy.Simulation.Game
{
    /// <summary>What a night's sleep came to, for the wake-up screen.</summary>
    public sealed class NightReport
    {
        public TimeSpan slept;
        public int steamers;   // every steamer gained while asleep (arrivals plus the free ones collected for you)
        public bool early;     // woke before morning
    }

    /// <summary>
    /// Turning in for the night (user request, 28 Sep 2026): between nightStartHour and nightEndHour the player can put
    /// the squishy to bed. Needs drain at nightDrain until morning (nightWakeByHour at the latest), and the free
    /// in-game steamers are collected for you and handed over on waking.
    /// </summary>
    public sealed partial class GameRules
    {
        /// <summary>Multiplies need drain (set per step by the caller: asleep overnight drains slower).</summary>
        public float DrainScale = 1;

        public bool IsNight(DateTime local) { return local.Hour >= R.nightStartHour || local.Hour < R.nightEndHour; }

        /// <summary>The night this moment belongs to, as a day number (after midnight still counts as the evening before).</summary>
        public static long NightKey(DateTime local) { return (local.Hour < 12 ? local.Date.AddDays(-1) : local.Date).Ticks / TimeSpan.TicksPerDay; }

        public bool CanSleep(DateTime local) { return !S.dead && !S.asleep && !S.tucked && IsNight(local); }

        /// <summary>Tucks it in for the night. Returns 1 if the free steamer waiting at bedtime was handed over.</summary>
        public int GoToSleep(DateTime utc, DateTime local)
        {
            if (S.asleep) return 0;
            S.asleep = true;
            S.sleepAt = utc.Ticks;
            var wake = local.Date.AddHours(R.nightWakeByHour);
            if (local.Hour >= R.nightEndHour) wake = wake.AddDays(1); // evening: tomorrow morning
            S.sleepUntil = utc.Ticks + (wake - local).Ticks;
            S.sleepSteamers = S.steamers;
            // The free steamer waiting now is handed over straight away; the night's own are banked for the morning.
            int now = OnlineReady() ? 1 : 0;
            if (now > 0) SetSteamers(S.steamers + 1);
            S.nightBank = 0;
            S.onlineReadyAt = NextReset(utc.Ticks, R.giftHours);
            S.lastNightPrompt = NightKey(local);
            return now;
        }

        /// <summary>Drain multiplier at a moment (used while catching up on time away).</summary>
        /// <summary>Asleep (for the night or tucked in): Rest fills instead of draining. Set with DrainScaleAt.</summary>
        public bool RestFill;

        public float DrainScaleAt(long utcTicks)
        {
            // Only a squishy you tucked in rests overnight: left up all night, it wakes needing care.
            bool asleep = S.asleep && utcTicks < S.sleepUntil;
            RestFill = S.tucked || asleep;
            float s = asleep ? R.nightDrain : 1;
            if (utcTicks < S.calmUntil) s *= R.calmDrain; // a quiet moment in the Quiet corner
            return s;
        }

        /// <summary>Steamers the night has brought so far (for the wake-up screen, before they're handed over).</summary>
        public int NightSteamersSoFar(DateTime utc)
        {
            if (!S.asleep) return 0;
            long end = Math.Min(utc.Ticks, S.sleepUntil);
            return S.nightBank + ResetsBetween(S.sleepAt, end, R.giftHours); // only the free ones the night brought (not ones earned playing after bedtime)
        }

        public NightReport WakeUp(DateTime utc)
        {
            if (!S.asleep) return null;
            long end = Math.Min(utc.Ticks, S.sleepUntil);
            int windows = ResetsBetween(S.sleepAt, end, R.giftHours); // the free one refilled (and was collected) at each reset in the night
            S.nightBank += windows;
            int got = S.nightBank;
            SetSteamers(S.steamers + got);
            var r = new NightReport { slept = TimeSpan.FromTicks(Math.Max(0, utc.Ticks - S.sleepAt)), steamers = got, early = utc.Ticks < S.sleepUntil - TimeSpan.FromHours(R.nightWakeByHour - R.nightEndHour).Ticks };
            S.onlineReadyAt = NextReset(end, R.giftHours);
            S.asleep = false;
            S.nightBank = 0;
            return r;
        }
    }
}
