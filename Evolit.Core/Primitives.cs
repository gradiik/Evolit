using System;

namespace Evolit.Core;

public readonly record struct CellId(long Value)
{
    public static CellId FromAxial(int q, int r)
    {
        return new CellId(unchecked(((long)q << 32) | (uint)r));
    }

    public int Q => unchecked((int)(Value >> 32));
    public int R => unchecked((int)Value);
    public override string ToString() => $"{Q}:{R}";
}

public readonly record struct OrganismId(long Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct GenomeId(int Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct LineageId(int Value)
{
    public override string ToString() => Value.ToString();
}

public enum SimulationMode
{
    Live = 0,
    Bootstrap = 1
}

public readonly record struct RandomState(ulong S0, ulong S1, ulong S2, ulong S3);

public static class SeedMixer
{
    public static ulong FromString(string value)
    {
        const ulong offset = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var ch in value ?? string.Empty)
        {
            hash ^= ch;
            hash *= prime;
        }
        return Mix(hash);
    }

    public static ulong Combine(ulong seed, ulong stream)
    {
        return Mix(seed ^ Mix(stream + 0x9E3779B97F4A7C15UL));
    }

    public static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}

public struct DeterministicRandom
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public DeterministicRandom(ulong seed)
    {
        var state = seed;
        _s0 = SplitMix64(ref state);
        _s1 = SplitMix64(ref state);
        _s2 = SplitMix64(ref state);
        _s3 = SplitMix64(ref state);

        if ((_s0 | _s1 | _s2 | _s3) == 0)
            _s0 = 1;
    }

    public RandomState CaptureState() => new(_s0, _s1, _s2, _s3);

    public void Restore(RandomState state)
    {
        _s0 = state.S0;
        _s1 = state.S1;
        _s2 = state.S2;
        _s3 = state.S3;
        if ((_s0 | _s1 | _s2 | _s3) == 0)
            _s0 = 1;
    }

    public ulong NextUInt64()
    {
        var result = RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);

        return result;
    }

    public uint NextUInt32() => (uint)(NextUInt64() >> 32);

    public double NextDouble()
    {
        return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
    }

    public float NextFloat() => (float)NextDouble();

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));

        return (int)(NextUInt64() % (uint)maxExclusive);
    }

    private static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong RotateLeft(ulong value, int shift)
    {
        return (value << shift) | (value >> (64 - shift));
    }
}

public sealed class CoreRandomStreams
{
    private DeterministicRandom _world;
    private DeterministicRandom _genetics;
    private DeterministicRandom _mutation;
    private DeterministicRandom _environment;
    private DeterministicRandom _organisms;

    public CoreRandomStreams(ulong seed)
    {
        _world = new DeterministicRandom(SeedMixer.Combine(seed, 1));
        _genetics = new DeterministicRandom(SeedMixer.Combine(seed, 2));
        _mutation = new DeterministicRandom(SeedMixer.Combine(seed, 3));
        _environment = new DeterministicRandom(SeedMixer.Combine(seed, 4));
        _organisms = new DeterministicRandom(SeedMixer.Combine(seed, 5));
    }

    public ref DeterministicRandom World => ref _world;
    public ref DeterministicRandom Genetics => ref _genetics;
    public ref DeterministicRandom Mutation => ref _mutation;
    public ref DeterministicRandom Environment => ref _environment;
    public ref DeterministicRandom Organisms => ref _organisms;

    public CoreRandomStreamsSnapshot CaptureSnapshot()
    {
        return new CoreRandomStreamsSnapshot
        {
            World = _world.CaptureState(),
            Genetics = _genetics.CaptureState(),
            Mutation = _mutation.CaptureState(),
            Environment = _environment.CaptureState(),
            Organisms = _organisms.CaptureState()
        };
    }

    public void Restore(CoreRandomStreamsSnapshot snapshot)
    {
        _world.Restore(snapshot.World);
        _genetics.Restore(snapshot.Genetics);
        _mutation.Restore(snapshot.Mutation);
        _environment.Restore(snapshot.Environment);
        _organisms.Restore(snapshot.Organisms);
    }
}

public sealed class CoreRandomStreamsSnapshot
{
    public RandomState World { get; set; }
    public RandomState Genetics { get; set; }
    public RandomState Mutation { get; set; }
    public RandomState Environment { get; set; }
    public RandomState Organisms { get; set; }
}
