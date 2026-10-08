using System.Numerics;

namespace CromoBound.Engine.Random;

/// <summary>The generator's full state, so a game can be saved and replayed exactly.</summary>
public readonly record struct RandomState(ulong S0, ulong S1, ulong S2, ulong S3);

/// <summary>xoshiro256** seeded through SplitMix64. Implemented here so replays never depend on the .NET version.</summary>
public sealed class SeededRandom
{
    private ulong _s0, _s1, _s2, _s3;

    public SeededRandom(ulong seed)
    {
        var x = seed;
        _s0 = SplitMix64(ref x);
        _s1 = SplitMix64(ref x);
        _s2 = SplitMix64(ref x);
        _s3 = SplitMix64(ref x);
    }

    public SeededRandom(RandomState state) => (_s0, _s1, _s2, _s3) = (state.S0, state.S1, state.S2, state.S3);

    public RandomState State => new(_s0, _s1, _s2, _s3);

    public ulong NextUInt64()
    {
        var result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Uniform integer in [0, <paramref name="maxExclusive"/>), without modulo bias.</summary>
    public int NextInt(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        var bound = (ulong)maxExclusive;
        var threshold = (0UL - bound) % bound;
        while (true)
        {
            var value = NextUInt64();
            if (value >= threshold) return (int)(value % bound);
        }
    }

    public int RollD20() => NextInt(20) + 1;

    /// <summary>Fisher-Yates shuffle in place, from the last index down.</summary>
    public void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15;
        var z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }
}
