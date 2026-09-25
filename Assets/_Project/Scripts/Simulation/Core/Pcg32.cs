using System;

namespace Squishy.Simulation.Core
{
    public interface IRandom
    {
        /// <summary>Uniform double in [0, 1).</summary>
        double NextDouble();

        /// <summary>Uniform int in [0, maxExclusive).</summary>
        int NextInt(int maxExclusive);
    }

    /// <summary>
    /// PCG32 (O'Neill, pcg-random.org). Used instead of System.Random because System.Random's
    /// sequence differs between Mono, IL2CPP and .NET, and gacha results must be reproducible.
    /// The whole state is one ulong, so it can be stored in the save.
    /// </summary>
    public sealed class Pcg32 : IRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Increment = 1442695040888963407UL;

        private ulong _state;

        public Pcg32(ulong seed)
        {
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        private Pcg32() { }

        /// <summary>Resume a generator from a saved <see cref="State"/>.</summary>
        public static Pcg32 FromState(ulong state)
        {
            var rng = new Pcg32();
            rng._state = state;
            return rng;
        }

        public ulong State { get { return _state; } }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + Increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        public double NextDouble()
        {
            // 32 random bits scaled into [0, 1).
            return NextUInt() * (1.0 / 4294967296.0);
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException("maxExclusive");
            return (int)(NextDouble() * maxExclusive);
        }
    }
}
