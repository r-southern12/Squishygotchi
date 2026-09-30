using System;
using System.Collections.Generic;

namespace Squishy.Simulation.Game
{
    [Serializable] public class LifeRecord { public string name, cause; public int finish, days, prestige, mistakes; public float qol; }
    [Serializable]
    public class CosmeticData
    {
        public string id, name, slot, kind, color;
        public int price;
        public string[] colors; // the colours the player can pick for it (empty: the shared accessory palette)

        public CosmeticData WithColor(string hex) { var c = (CosmeticData)MemberwiseClone(); c.color = hex; return c; }
    }
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

        /// <summary>What this life would earn at old age if care stays as it has been (care mistakes so far included).</summary>
        public int ProjectedPrestige() { return (int)Math.Round((R.prestigeBase + R.prestigePerQol * QualityOfLife()) * MistakeFactor()); }

        /// <summary>What's left of the old-age prestige after this life's care mistakes (5% each, never below half).</summary>
        public float MistakeFactor() { return Math.Max(R.careMistakeFloor, 1 - R.careMistakePenalty * S.careMistakes); }

        /// <summary>
        /// Growing up well: a little prestige on reaching each new life stage, scaled by the care given so far. Most
        /// prestige still comes at the end of a full life. Returns what was paid (0 if nothing new).
        /// </summary>
        public int AwardStagePrestige()
        {
            int reached = (int)LifeStage(), award = 0;
            if (R.stagePrestige == null) return 0;
            while (S.stageAwarded < reached && S.stageAwarded < R.stagePrestige.Length)
            {
                award += (int)Math.Round(R.stagePrestige[S.stageAwarded] * QualityOfLife());
                S.stageAwarded++;
            }
            S.prestige += award;
            return award;
        }

        public bool ReachedOldAge() { return !S.dead && S.age >= ExpectedLifespanDays(); }

        /// <summary>Ends this life. Old age earns prestige from quality of life; neglect earns none.</summary>
        public LifeRecord EndLife(bool oldAge, string cause)
        {
            float q = QualityOfLife();
            int award = oldAge ? (int)Math.Round((R.prestigeBase + R.prestigePerQol * q) * MistakeFactor()) : 0;
            var rec = new LifeRecord { name = Fav.name, finish = S.favIdx, days = S.age, qol = q, cause = cause, prestige = award, mistakes = S.careMistakes };
            S.lives.Add(rec);
            S.prestige += award;
            S.dead = true;
            return rec;
        }

        /// <summary>Starts the next squishy's life (the favourite changes; the room and belongings carry over).</summary>
        /// <param name="keepCopy">A full life ends with a baby of the same type: the collection keeps its copies (and size).</param>
        public void StartLife(int next, bool keepCopy = false)
        {
            if (!keepCopy) RemoveSquish(S.favIdx);
            if (SquishCount(next) == 0) SetSquish(next, 1);
            S.lifeOf.RemoveAll(l => l.i == next || l.i == S.favIdx); // a fresh life for the newcomer; the old one is over
            S.favIdx = next;
            S.dead = false;
            S.deathClock = 0;
            S.age = 1;
            S.dayT = 0;
            S.qolSum = 0;
            S.qolTime = 0;
            ResetCareMistakes(0);
            S.tucked = false;
            for (int k = 0; k < 4; k++) S.needs[k] = .75f;
            S.stageAwarded = 0;
        }

        /// <summary>
        /// Makes another squishy the favourite. Each type keeps its own life (age, quality of life, stage
        /// prestige): the current one's is put aside and the other's resumes, or begins as a baby. Needs belong to
        /// the room, so swapping never escapes neglect.
        /// </summary>
        public void SwapFavourite(int next)
        {
            if (next == S.favIdx || SquishCount(next) == 0) return;
            S.lifeOf.RemoveAll(l => l.i == S.favIdx);
            S.lifeOf.Add(new LifeState { i = S.favIdx, age = S.age, dayT = S.dayT, qolSum = S.qolSum, qolTime = S.qolTime, stageAwarded = S.stageAwarded, careMistakes = S.careMistakes });
            var mine = S.lifeOf.Find(l => l.i == next);
            if (mine != null) S.lifeOf.Remove(mine);
            else mine = new LifeState { i = next, age = 1 };
            S.favIdx = next;
            S.age = Math.Max(1, mine.age);
            S.dayT = mine.dayT;
            S.qolSum = mine.qolSum;
            S.qolTime = mine.qolTime;
            S.stageAwarded = mine.stageAwarded;
            S.careMistakes = mine.careMistakes; // the room's needs carry over, so an empty spell in progress keeps counting
        }

        private void ResetCareMistakes(int n)
        {
            S.careMistakes = n;
            S.emptyFor = new float[4];
            S.mistakeCounted = new bool[4];
        }

        /// <summary>Care mistakes made since last asked (for a note in the game), and which need the last one was.</summary>
        public int NewMistakes, LastMistakeNeed = -1;

        /// <summary>
        /// Care mistakes (user request, 1 Oct 2026): a need sitting empty for careMistakeSeconds while awake is one
        /// mistake, counted once until that need is looked after again. Called from StepCare.
        /// </summary>
        private void StepCareMistakes(float sdt, bool asleep)
        {
            if (S.emptyFor == null || S.emptyFor.Length != 4) S.emptyFor = new float[4];
            if (S.mistakeCounted == null || S.mistakeCounted.Length != 4) S.mistakeCounted = new bool[4];
            for (int k = 0; k < 4; k++)
            {
                if (S.needs[k] > 0f) S.emptyFor[k] = 0;
                if (S.needs[k] > R.careMistakeReset) S.mistakeCounted[k] = false;
                if (S.needs[k] > 0f || asleep || R.careMistakeSeconds <= 0) continue;
                S.emptyFor[k] += sdt;
                if (!S.mistakeCounted[k] && S.emptyFor[k] >= R.careMistakeSeconds)
                {
                    S.mistakeCounted[k] = true;
                    S.careMistakes++;
                    NewMistakes++;
                    LastMistakeNeed = k;
                }
            }
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

        /// <summary>The colours an accessory comes in: its own first, then its palette (or the shared one).</summary>
        public List<string> CosmeticChoices(CosmeticData c)
        {
            var l = new List<string> { c.color };
            var pal = c.colors != null && c.colors.Length > 0 ? c.colors : R.cosmeticColors;
            if (pal != null) foreach (var h in pal) if (!l.Contains(h)) l.Add(h);
            return l;
        }

        /// <summary>The colour the player picked for an accessory (its own colour until they pick another).</summary>
        public string CosmeticColor(string id)
        {
            var c = Cosmetic(id);
            if (c == null) return null;
            var e = S.cosColors.Find(x => x.id == id);
            return e != null && CosmeticChoices(c).Contains(e.color) ? e.color : c.color;
        }

        /// <summary>Picks a colour for an owned accessory (one of its choices). Any time, as often as you like.</summary>
        public bool SetCosmeticColor(string id, string hex)
        {
            var c = Cosmetic(id);
            if (c == null || !HasCosmetic(id) || !CosmeticChoices(c).Contains(hex)) return false;
            S.cosColors.RemoveAll(x => x.id == id);
            if (hex != c.color) S.cosColors.Add(new CosColor { id = id, color = hex });
            return true;
        }

        /// <summary>An accessory as worn: in the colour the player picked.</summary>
        public CosmeticData Worn(string id)
        {
            var c = Cosmetic(id);
            if (c == null) return null;
            string col = CosmeticColor(id);
            return col == c.color ? c : c.WithColor(col);
        }

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

        // Every giftHours, besides the steamer each care task pays: one is free to claim when you're in the game,
        // and one more is a bonus for an optional video (included with the full game). Nothing arrives on its own.
        // Both refill at fixed resets on the clock, like the care tasks, so the wait is often less than the window.

        /// <summary>The first reset after this moment: every few hours on the local clock (midnight, 3am, 6am... for 3).</summary>
        public static long NextReset(long afterUtcTicks, double hours)
        {
            var local = new DateTime(afterUtcTicks, DateTimeKind.Utc).ToLocalTime();
            double h = (local - local.Date).TotalHours;
            return local.Date.AddHours((Math.Floor(h / hours + 1e-9) + 1) * hours).ToUniversalTime().Ticks;
        }

        /// <summary>How many resets fall after one moment, up to and including another.</summary>
        public static int ResetsBetween(long fromUtcTicks, long toUtcTicks, double hours)
        {
            int n = 0;
            for (long b = NextReset(fromUtcTicks, hours); b <= toUtcTicks; b = NextReset(b, hours)) n++;
            return n;
        }

        /// <summary>The free one you claim by being in the game.</summary>
        public bool OnlineReady() { return S.onlineReadyAt <= Clock.UtcNow.Ticks; }
        public TimeSpan OnlineWait() { return TimeSpan.FromTicks(Math.Max(0, S.onlineReadyAt - Clock.UtcNow.Ticks)); }

        public bool ClaimOnline()
        {
            if (!OnlineReady()) return false;
            SetSteamers(S.steamers + 1);
            S.onlineReadyAt = NextReset(Clock.UtcNow.Ticks, R.giftHours);
            return true;
        }

        public bool BonusReady() { return S.bonusReadyAt <= Clock.UtcNow.Ticks; }
        public TimeSpan BonusWait() { return TimeSpan.FromTicks(Math.Max(0, S.bonusReadyAt - Clock.UtcNow.Ticks)); }

        public bool ClaimBonus()
        {
            if (!BonusReady()) return false;
            SetSteamers(S.steamers + 1);
            S.bonusReadyAt = NextReset(Clock.UtcNow.Ticks, R.giftHours);
            return true;
        }

        // ---- daily streak and the missions goal ----

        /// <summary>Called when a task is claimed. Returns a message when a streak or goal reward is earned.</summary>
        public string RecordTaskDone()
        {
            var today = Clock.UtcNow.ToLocalTime().Date;
            var last = new DateTime(S.lastTaskDay);
            string msg = null;
            if (last != today)
            {
                S.streak = last == today.AddDays(-1) ? S.streak + 1 : 1;
                S.lastTaskDay = today.Ticks;
                if (StreakBonus() > 0) msg = S.streak + "-day streak! Missions pay +" + (int)Math.Round(StreakBonus() * 100) + "% coins";
            }
            var start = GoalStart(today);
            if (S.weekStart != start.Ticks) { S.weekStart = start.Ticks; S.weekTasks = 0; S.weekClaimed = false; }
            S.weekTasks++;
            if (!S.weekClaimed && S.weekTasks >= R.goalMissions)
            {
                S.weekClaimed = true;
                SetSteamers(S.steamers + R.goalSteamers);
                msg = "Missions goal done! +" + R.goalSteamers + " steamers";
            }
            return msg;
        }

        /// <summary>The streak as it stands now: days in a row with a mission, broken if yesterday was missed.</summary>
        public int CurrentStreak()
        {
            var today = Clock.UtcNow.ToLocalTime().Date;
            var last = new DateTime(S.lastTaskDay);
            return last == today || last == today.AddDays(-1) ? S.streak : 0;
        }

        /// <summary>A streak is a coin bonus on missions (not steamers): each day after the first adds a little, up to a cap.</summary>
        public float StreakBonus() { return Math.Min(R.streakBonusMax, Math.Max(0, CurrentStreak() - 1) * R.streakBonusPerDay); }

        /// <summary>The streak the next mission counts in (today's first mission after yesterday's adds a day).</summary>
        public int UpcomingStreak()
        {
            var today = Clock.UtcNow.ToLocalTime().Date;
            var last = new DateTime(S.lastTaskDay);
            return last == today ? S.streak : last == today.AddDays(-1) ? S.streak + 1 : 1;
        }

        /// <summary>The coin bonus the next mission will pay.</summary>
        public float UpcomingStreakBonus() { return Math.Min(R.streakBonusMax, Math.Max(0, UpcomingStreak() - 1) * R.streakBonusPerDay); }

        /// <summary>What a mission will pay when claimed, streak bonus included.</summary>
        public int UpcomingTaskCoins(TaskData d) { return (int)Math.Round(d.coins * (1 + UpcomingStreakBonus())); }

        /// <summary>What a mission pays with the streak bonus.</summary>
        public int TaskCoins(TaskData d) { return (int)Math.Round(d.coins * (1 + StreakBonus())); }

        /// <summary>Missions done towards the current goal.</summary>
        public int GoalTasks() { return S.weekStart == GoalStart(Clock.UtcNow.ToLocalTime().Date).Ticks ? S.weekTasks : 0; }

        /// <summary>Whether the current goal has been reached (and paid).</summary>
        public bool GoalDone() { return GoalTasks() >= R.goalMissions && S.weekClaimed; }

        /// <summary>
        /// The missions goal starts afresh on set days of the week (Monday and Thursday, data), so there's a new one
        /// midweek as well as at the start of the week.
        /// </summary>
        public DateTime GoalStart(DateTime localDay)
        {
            var d = localDay.Date;
            for (int k = 0; k < 7; k++, d = d.AddDays(-1)) if (IsGoalDay(d)) return d;
            return localDay.Date;
        }

        /// <summary>When the next goal starts (local date).</summary>
        public DateTime NextGoalStart()
        {
            var d = Clock.UtcNow.ToLocalTime().Date.AddDays(1);
            for (int k = 0; k < 7; k++, d = d.AddDays(1)) if (IsGoalDay(d)) return d;
            return d;
        }

        private bool IsGoalDay(DateTime d)
        {
            var days = R.goalResetDays != null && R.goalResetDays.Length > 0 ? R.goalResetDays : new[] { 1 };
            return Array.IndexOf(days, (int)d.DayOfWeek) >= 0;
        }

        // ---- collection tree ----

        public static readonly string[] TreeTiers = { "Common", "Glitter", "Pattern", "Galaxy", "UV", "Holographic", "Aquarium", "Legendary" };

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
