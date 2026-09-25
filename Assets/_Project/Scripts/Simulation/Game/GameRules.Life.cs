using System;
using System.Collections.Generic;

namespace Squishy.Simulation.Game
{
    [Serializable] public class LifeRecord { public string name, cause; public int finish, days, prestige; public float qol; }
    [Serializable] public class CosmeticData { public string id, name, slot, kind, color; public int price; }
    [Serializable] public class TierRewardData { public string tier; public int steamers, prestige; }

    /// <summary>
    /// Life cycle and meta progression: quality of life, lifespan, life stages, old age and prestige, prestige
    /// cosmetics, the free trial, the gift steamer, daily streaks and weekly goals, the collection tree and event styles.
    /// </summary>
    public sealed partial class GameRules
    {
        public enum Life { Baby, Young, Adult, Elder }

        // ---- quality of life and lifespan ----

        /// <summary>Average condition across this life (0..1): the quality-of-life score.</summary>
        public float QualityOfLife() { return S.qolTime > 0 ? S.qolSum / S.qolTime : 1f; }

        /// <summary>Good care stretches life from the minimum towards the maximum.</summary>
        public float ExpectedLifespanDays() { return R.lifespanMinDays + (R.lifespanMaxDays - R.lifespanMinDays) * QualityOfLife(); }

        public Life LifeStage()
        {
            if (S.age <= R.babyDays) return Life.Baby;
            float f = S.age / ExpectedLifespanDays();
            return f < .35f ? Life.Young : f < .8f ? Life.Adult : Life.Elder;
        }

        public static string StageName(Life s) { return s == Life.Baby ? "Baby" : s == Life.Young ? "Young" : s == Life.Adult ? "Adult" : "Elder"; }

        /// <summary>What this life would earn at old age if care stays as it has been.</summary>
        public int ProjectedPrestige() { return (int)Math.Round(R.prestigeBase + R.prestigePerQol * QualityOfLife()); }

        public bool ReachedOldAge() { return !S.dead && S.age >= ExpectedLifespanDays(); }

        /// <summary>Ends this life. Old age earns prestige from quality of life; neglect earns none.</summary>
        public LifeRecord EndLife(bool oldAge, string cause)
        {
            float q = QualityOfLife();
            int award = oldAge ? (int)Math.Round(R.prestigeBase + R.prestigePerQol * q) : 0;
            var rec = new LifeRecord { name = Fav.name, finish = S.favIdx, days = S.age, qol = q, cause = cause, prestige = award };
            S.lives.Add(rec);
            S.prestige += award;
            S.dead = true;
            return rec;
        }

        /// <summary>Starts the next squishy's life (the favourite changes; the room and belongings carry over).</summary>
        public void StartLife(int next)
        {
            RemoveSquish(S.favIdx);
            if (SquishCount(next) == 0) SetSquish(next, 1);
            S.favIdx = next;
            S.dead = false;
            S.deathClock = 0;
            S.age = 1;
            S.dayT = 0;
            S.qolSum = 0;
            S.qolTime = 0;
            S.tucked = false;
            for (int k = 0; k < 4; k++) S.needs[k] = .75f;
        }

        /// <summary>Total prestige earned and average quality of life across finished lives (the lifetime tally).</summary>
        public float LifetimeQol()
        {
            if (S.lives.Count == 0) return 0;
            float t = 0;
            foreach (var l in S.lives) t += l.qol;
            return t / S.lives.Count;
        }

        // ---- prestige cosmetics ----

        public CosmeticData Cosmetic(string id) { foreach (var c in C.cosmetics) if (c.id == id) return c; return null; }
        public bool HasCosmetic(string id) { return S.cosmetics.Contains(id); }

        public bool BuyCosmetic(string id)
        {
            var c = Cosmetic(id);
            if (c == null || HasCosmetic(id) || S.prestige < c.price) return false;
            S.prestige -= c.price;
            S.cosmetics.Add(id);
            Equip(id);
            return true;
        }

        /// <summary>Wears a cosmetic in its slot, or takes it off if already worn.</summary>
        public void Equip(string id)
        {
            var c = Cosmetic(id);
            if (c == null || !HasCosmetic(id)) return;
            if (c.slot == "hat") S.hat = S.hat == id ? "" : id;
            else if (c.slot == "neck") S.neck = S.neck == id ? "" : id;
            else S.face = S.face == id ? "" : id;
        }

        // ---- free trial and premium ----

        /// <summary>Free players get one squishy life or the trial period, whichever ends first.</summary>
        public bool TrialOver()
        {
            if (S.premium) return false;
            if (S.lives.Count >= 1) return true;
            return Clock.UtcNow.Ticks - S.trialStart > TimeSpan.FromDays(R.trialDays).Ticks;
        }

        public TimeSpan TrialLeft() { return TimeSpan.FromTicks(Math.Max(0, S.trialStart + TimeSpan.FromDays(R.trialDays).Ticks - Clock.UtcNow.Ticks)); }

        // ---- gift steamer ----

        public bool GiftReady() { return S.giftReadyAt <= Clock.UtcNow.Ticks; }
        public TimeSpan GiftWait() { return TimeSpan.FromTicks(Math.Max(0, S.giftReadyAt - Clock.UtcNow.Ticks)); }

        public void ClaimGift()
        {
            if (!GiftReady()) return;
            SetSteamers(S.steamers + 1);
            S.giftReadyAt = Clock.UtcNow.Ticks + TimeSpan.FromHours(R.giftHours).Ticks;
        }

        // ---- daily streak and weekly goal ----

        /// <summary>Called when a task is claimed. Returns a message when a streak or weekly reward is earned.</summary>
        public string RecordTaskDone()
        {
            var today = Clock.UtcNow.ToLocalTime().Date;
            var last = new DateTime(S.lastTaskDay);
            string msg = null;
            if (last != today)
            {
                S.streak = last == today.AddDays(-1) ? S.streak + 1 : 1;
                S.lastTaskDay = today.Ticks;
                if (S.streak % 7 == 0) { SetSteamers(S.steamers + 2); S.prestige += R.streakWeekPrestige; msg = S.streak + "-day streak! +2 steamers, +" + R.streakWeekPrestige + " prestige"; }
                else if (S.streak % 7 == 3) { SetSteamers(S.steamers + 1); msg = "3-day streak! +1 steamer"; }
            }
            var week = WeekStart(today);
            if (S.weekStart != week.Ticks) { S.weekStart = week.Ticks; S.weekTasks = 0; S.weekClaimed = false; }
            S.weekTasks++;
            if (!S.weekClaimed && S.weekTasks >= R.weeklyGoal)
            {
                S.weekClaimed = true;
                SetSteamers(S.steamers + R.weeklySteamers);
                msg = "Weekly goal done! +" + R.weeklySteamers + " steamers";
            }
            return msg;
        }

        public int WeekTasks() { return S.weekStart == WeekStart(Clock.UtcNow.ToLocalTime().Date).Ticks ? S.weekTasks : 0; }

        private static DateTime WeekStart(DateTime d) { return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); }

        // ---- collection tree ----

        public static readonly string[] TreeTiers = { "Common", "Glitter", "Pattern", "Galaxy", "UV", "Holographic", "Legendary" };

        public int TierOwned(string tier, out int total)
        {
            int own = 0;
            total = 0;
            for (int i = 0; i < C.finishes.Length; i++)
            {
                if (C.finishes[i].tier != tier) continue;
                total++;
                if (SquishCount(i) > 0 || S.lives.Exists(l => l.finish == i)) own++;
            }
            return own;
        }

        public TierRewardData TierReward(string tier) { foreach (var t in C.tierRewards) if (t.tier == tier) return t; return null; }
        public bool TierClaimed(string tier) { return S.tiersClaimed.Contains(tier); }

        /// <summary>Completing a finish tier pays a one-off reward.</summary>
        public bool ClaimTier(string tier)
        {
            int total;
            var rw = TierReward(tier);
            if (rw == null || TierClaimed(tier) || TierOwned(tier, out total) < total) return false;
            S.tiersClaimed.Add(tier);
            SetSteamers(S.steamers + rw.steamers);
            S.prestige += rw.prestige;
            return true;
        }

        // ---- event styles ----

        /// <summary>Launch styles are always in steamers; event styles only in their months.</summary>
        public bool StyleActive(StyleData s)
        {
            if (s.eventMonths == null || s.eventMonths.Length == 0) return true;
            int m = Clock.UtcNow.ToLocalTime().Month;
            return Array.IndexOf(s.eventMonths, m) >= 0;
        }

        // ---- clock guard ----

        /// <summary>Returns the trusted "now": never earlier than the last time seen (stops winding the clock back).</summary>
        public DateTime TrustedNow(DateTime deviceNow)
        {
            if (deviceNow.Ticks < S.lastSeenUtc) deviceNow = new DateTime(S.lastSeenUtc, DateTimeKind.Utc);
            S.lastSeenUtc = deviceNow.Ticks;
            return deviceNow;
        }
    }
}
