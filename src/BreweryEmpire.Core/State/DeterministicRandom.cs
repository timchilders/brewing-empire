using System;

namespace BreweryEmpire.Core.State
{
    /// <summary>
    /// Seeded, serializable, platform-stable PRNG (xorshift128+).
    ///
    /// DETERMINISM CONTRACT: this is the ONLY source of randomness permitted
    /// anywhere in BreweryEmpire.Core. System.Random is not platform-stable
    /// across runtimes and UnityEngine.Random is both unavailable here and
    /// unseedable per-instance. A guard test enforces their absence.
    ///
    /// The full generator state is exposed so it can be serialized inside
    /// GameState, making save/load round-trips bit-exact.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _s0;
        private ulong _s1;

        public DeterministicRandom(int seed)
        {
            // SplitMix64 the seed so that small/sequential seeds still produce
            // well-separated starting states.
            ulong x = unchecked((ulong)seed + 0x9E3779B97F4A7C15UL);
            _s0 = SplitMix64(ref x);
            _s1 = SplitMix64(ref x);

            // xorshift128+ degenerates permanently if both words are zero.
            if (_s0 == 0 && _s1 == 0) _s1 = 0x9E3779B97F4A7C15UL;
        }

        private DeterministicRandom(ulong s0, ulong s1)
        {
            _s0 = s0;
            _s1 = s1;
        }

        private static ulong SplitMix64(ref ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                ulong z = x;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Full generator state, for serialization. Always length 2.</summary>
        public ulong[] State => new[] { _s0, _s1 };

        public static DeterministicRandom FromState(ulong[] state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Length != 2)
                throw new ArgumentException("State must have exactly 2 elements.", nameof(state));
            if (state[0] == 0 && state[1] == 0)
                throw new ArgumentException("State must not be all zero.", nameof(state));
            return new DeterministicRandom(state[0], state[1]);
        }

        /// <summary>Independent copy at the current position.</summary>
        public DeterministicRandom Clone() => new DeterministicRandom(_s0, _s1);

        /// <summary>Raw 64-bit draw (xorshift128+).</summary>
        public ulong NextULong()
        {
            unchecked
            {
                ulong x = _s0;
                ulong y = _s1;
                _s0 = y;
                x ^= x << 23;
                _s1 = x ^ y ^ (x >> 17) ^ (y >> 26);
                return _s1 + y;
            }
        }

        /// <summary>
        /// Uniform integer in [minInclusive, maxExclusive).
        /// Uses rejection sampling: modulo bias would quietly skew balance
        /// tuning in a way that is very hard to notice and harder to debug.
        /// </summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");

            ulong range = (ulong)((long)maxExclusive - minInclusive);

            // Largest multiple of range that fits in 2^64; draws at or above
            // this are rejected to keep the distribution exactly uniform.
            ulong limit = ulong.MaxValue - (ulong.MaxValue % range);

            ulong draw;
            do
            {
                draw = NextULong();
            } while (draw >= limit);

            return (int)((long)minInclusive + (long)(draw % range));
        }

        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive) => NextInt(0, maxExclusive);

        /// <summary>
        /// True with probability basisPoints/10000.
        /// Basis points keep probability integral and serialization exact —
        /// infection rolls, quality variance and container loss all use this.
        /// </summary>
        public bool Chance(int basisPoints)
        {
            if (basisPoints <= 0) return false;
            if (basisPoints >= 10000) return true;
            return NextInt(0, 10000) < basisPoints;
        }

        /// <summary>
        /// Uniform integer in [center - spread, center + spread].
        /// The common shape for quality and yield variance.
        /// </summary>
        public int Variance(int center, int spread)
        {
            if (spread < 0) throw new ArgumentOutOfRangeException(nameof(spread));
            if (spread == 0) return center;
            return center + NextInt(-spread, spread + 1);
        }
    }
}
