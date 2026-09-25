using System;
using Squishy.Simulation.Content;

namespace Squishy.Simulation.Care
{
    public enum CareStage
    {
        Happy = 0,
        Droopy = 1,
        Flat = 2,
        Critical = 3,
        Dead = 4,
    }

    /// <summary>The favourite's needs and life. Saved; carried across a favourite swap.</summary>
    [Serializable]
    public class CareState
    {
        public float hunger = 1f;
        public float play = 1f;
        public float rest = 1f;
        public float clean = 1f;

        public float secondsAtZero;
        public float ageSeconds;
        public int generation = 1;
        public bool dead;
        public NeedKind causeOfDeath;

        public float Get(NeedKind need)
        {
            switch (need)
            {
                case NeedKind.Hunger: return hunger;
                case NeedKind.Play: return play;
                case NeedKind.Rest: return rest;
                default: return clean;
            }
        }

        public void Set(NeedKind need, float value)
        {
            value = value < 0f ? 0f : (value > 1f ? 1f : value);
            switch (need)
            {
                case NeedKind.Hunger: hunger = value; break;
                case NeedKind.Play: play = value; break;
                case NeedKind.Rest: rest = value; break;
                default: clean = value; break;
            }
        }
    }

    /// <summary>Rules for needs, self-care limits, decline and death. Pure functions over <see cref="CareState"/>.</summary>
    public static class CareSim
    {
        public static readonly NeedKind[] AllNeeds = { NeedKind.Hunger, NeedKind.Play, NeedKind.Rest, NeedKind.Clean };

        /// <summary>The lowest need decides the squishy's condition.</summary>
        public static float Condition(CareState s)
        {
            return Math.Min(Math.Min(s.hunger, s.play), Math.Min(s.rest, s.clean));
        }

        public static NeedKind Lowest(CareState s)
        {
            NeedKind lowest = NeedKind.Hunger;
            for (int i = 1; i < AllNeeds.Length; i++)
                if (s.Get(AllNeeds[i]) < s.Get(lowest)) lowest = AllNeeds[i];
            return lowest;
        }

        public static CareStage Stage(CareState s, CareDef def)
        {
            if (s.dead) return CareStage.Dead;
            float c = Condition(s);
            if (c > def.happyAbove) return CareStage.Happy;
            if (c > def.droopyAbove) return CareStage.Droopy;
            if (c >= def.criticalBelow) return CareStage.Flat;
            return CareStage.Critical;
        }

        /// <summary>Too weak to look after itself; only the player can help.</summary>
        public static bool CanSelfCare(CareState s, CareDef def)
        {
            return !s.dead && Condition(s) >= def.criticalBelow;
        }

        /// <summary>Drain slowdown from Comfort, 0..max.</summary>
        public static float ComfortSlowdown(int comfort, EconomyDef economy)
        {
            return Math.Min(economy.maxComfortDrainReduction, Math.Max(0, comfort) * economy.comfortDrainPerPoint);
        }

        /// <summary>
        /// Advances time: drains needs, ages the squishy and runs the death clock.
        /// The clock runs while any need is at zero and winds back down otherwise.
        /// Returns true on the tick the squishy dies.
        /// </summary>
        public static bool Tick(CareState s, CareDef def, float seconds, float comfortSlowdown)
        {
            if (s.dead || seconds <= 0f) return false;
            float k = 1f - comfortSlowdown;
            bool anyZero = false;
            for (int i = 0; i < AllNeeds.Length; i++)
            {
                var n = AllNeeds[i];
                float v = s.Get(n) - def.DrainPerSecond(n) * k * seconds;
                s.Set(n, v);
                if (s.Get(n) <= 0f) anyZero = true;
            }
            s.ageSeconds += seconds;

            if (anyZero) s.secondsAtZero += seconds;
            else s.secondsAtZero = Math.Max(0f, s.secondsAtZero - seconds);

            if (s.secondsAtZero > def.deathSecondsAtZero)
            {
                s.dead = true;
                s.causeOfDeath = Lowest(s);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Fills a need, never above <paramref name="cap"/> and never lowering it.
        /// Self-care passes CareDef.selfCareCap (or lower) so autonomy can't make the squishy thrive.
        /// </summary>
        public static void Fill(CareState s, NeedKind need, float amount, float cap)
        {
            float v = s.Get(need);
            if (v >= cap || amount <= 0f) return;
            s.Set(need, Math.Min(cap, v + amount));
        }

        /// <summary>Cap for an activity: the player can fill up to the activity's cap, self-care only to the autonomy cap.</summary>
        public static float CapFor(ActivityDef activity, bool byPlayer, CareDef def)
        {
            return byPlayer ? activity.playerCap : Math.Min(activity.playerCap, def.selfCareCap);
        }

        /// <summary>
        /// Catches up after the app was closed. Drains in steps, and between steps the squishy
        /// self-cares its low needs at an average rate (capped at the autonomy cap) while it's able to.
        /// </summary>
        public static bool SimulateOffline(CareState s, CareDef def, double seconds, float comfortSlowdown)
        {
            if (s.dead || seconds <= 0) return false;
            double remaining = Math.Min(seconds, def.maxOfflineSeconds);
            float step = Math.Max(1f, def.offlineStepSeconds);
            bool died = false;
            while (remaining > 0 && !s.dead)
            {
                float dt = (float)Math.Min(step, remaining);
                remaining -= dt;
                died = Tick(s, def, dt, comfortSlowdown);
                if (s.dead || !CanSelfCare(s, def)) continue;
                for (int i = 0; i < AllNeeds.Length; i++)
                    if (s.Get(AllNeeds[i]) < def.selfCareBelow)
                        Fill(s, AllNeeds[i], def.offlineSelfCareRate * dt, def.selfCareCap);
            }
            return died;
        }

        /// <summary>A new generation after death: fresh needs, age reset. Everything else carries over.</summary>
        public static void StartNextGeneration(CareState s)
        {
            int generation = s.generation + 1;
            s.hunger = s.play = s.rest = s.clean = 1f;
            s.secondsAtZero = 0f;
            s.ageSeconds = 0f;
            s.dead = false;
            s.generation = generation;
        }
    }
}
