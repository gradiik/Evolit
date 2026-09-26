using System;
using System.Collections.Generic;

namespace Evolit.Core;

public readonly record struct Genome(
    float BodyScalePotential,
    float MetabolicTendency,
    float MovementPotential,
    float ThermalPreference,
    float ThermalTolerance,
    float MoistureTolerance,
    float ReproductionInvestment)
{
    public static Genome Create(
        float bodyScalePotential,
        float metabolicTendency,
        float movementPotential,
        float thermalPreference,
        float thermalTolerance,
        float moistureTolerance,
        float reproductionInvestment)
    {
        return new Genome(
            Clamp01(bodyScalePotential),
            Clamp01(metabolicTendency),
            Clamp01(movementPotential),
            Clamp01(thermalPreference),
            Clamp01(thermalTolerance),
            Clamp01(moistureTolerance),
            Clamp01(reproductionInvestment));
    }

    private static float Clamp01(float value)
    {
        if (!float.IsFinite(value))
            return 0.5f;
        return Math.Clamp(value, 0f, 1f);
    }
}

public readonly record struct Phenotype(
    float BodyScale,
    float MetabolicRate,
    float MovementCapacity,
    float PreferredTemperatureCelsius,
    float TemperatureToleranceCelsius,
    float MoistureTolerance,
    float ReproductionInvestment);

public static class PhenotypeCompiler
{
    public static Phenotype Compile(Genome genome)
    {
        return new Phenotype(
            0.20f + genome.BodyScalePotential * 3.80f,
            0.45f + genome.MetabolicTendency * 1.75f,
            0.10f + genome.MovementPotential * 1.90f,
            -15f + genome.ThermalPreference * 60f,
            2f + genome.ThermalTolerance * 28f,
            genome.MoistureTolerance,
            genome.ReproductionInvestment);
    }
}

public sealed class GenomeStore
{
    private Genome[] _items = new Genome[16];
    private int _count;

    public int Count => _count;

    public GenomeId Add(Genome genome)
    {
        EnsureCapacity(_count + 1);
        _items[_count] = genome;
        _count++;
        return new GenomeId(_count);
    }

    public Genome Get(GenomeId id)
    {
        var index = id.Value - 1;
        if ((uint)index >= (uint)_count)
            throw new KeyNotFoundException($"Unknown genome {id.Value}.");
        return _items[index];
    }

    public GenomeSnapshot CaptureSnapshot()
    {
        var items = new Genome[_count];
        Array.Copy(_items, items, _count);
        return new GenomeSnapshot { Items = items };
    }

    public static GenomeStore Restore(GenomeSnapshot snapshot)
    {
        var store = new GenomeStore();
        var source = snapshot.Items ?? Array.Empty<Genome>();
        store.EnsureCapacity(source.Length);
        Array.Copy(source, store._items, source.Length);
        store._count = source.Length;
        return store;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _items.Length)
            return;
        var next = Math.Max(required, _items.Length * 2);
        Array.Resize(ref _items, next);
    }
}

public sealed class GenomeSnapshot
{
    public Genome[] Items { get; set; } = Array.Empty<Genome>();
}

public readonly record struct MutationSettings(float Probability, float Strength)
{
    public static MutationSettings FoundationDefault => new(0.08f, 0.06f);

    public MutationSettings Normalized()
    {
        return new MutationSettings(
            Math.Clamp(float.IsFinite(Probability) ? Probability : 0f, 0f, 1f),
            Math.Clamp(float.IsFinite(Strength) ? Strength : 0f, 0f, 1f));
    }
}

public static class GenomeInheritance
{
    public static GenomeId CreateOffspring(
        GenomeStore store,
        GenomeId parentA,
        GenomeId? parentB,
        ref DeterministicRandom random,
        MutationSettings mutation)
    {
        ArgumentNullException.ThrowIfNull(store);
        var a = store.Get(parentA);
        var baseGenome = parentB.HasValue
            ? Blend(a, store.Get(parentB.Value), ref random)
            : a;
        return store.Add(Mutate(baseGenome, ref random, mutation.Normalized()));
    }

    public static Genome Mutate(
        Genome genome,
        ref DeterministicRandom random,
        MutationSettings settings)
    {
        return Genome.Create(
            MutateValue(genome.BodyScalePotential, ref random, settings),
            MutateValue(genome.MetabolicTendency, ref random, settings),
            MutateValue(genome.MovementPotential, ref random, settings),
            MutateValue(genome.ThermalPreference, ref random, settings),
            MutateValue(genome.ThermalTolerance, ref random, settings),
            MutateValue(genome.MoistureTolerance, ref random, settings),
            MutateValue(genome.ReproductionInvestment, ref random, settings));
    }

    private static Genome Blend(Genome a, Genome b, ref DeterministicRandom random)
    {
        return Genome.Create(
            BlendValue(a.BodyScalePotential, b.BodyScalePotential, ref random),
            BlendValue(a.MetabolicTendency, b.MetabolicTendency, ref random),
            BlendValue(a.MovementPotential, b.MovementPotential, ref random),
            BlendValue(a.ThermalPreference, b.ThermalPreference, ref random),
            BlendValue(a.ThermalTolerance, b.ThermalTolerance, ref random),
            BlendValue(a.MoistureTolerance, b.MoistureTolerance, ref random),
            BlendValue(a.ReproductionInvestment, b.ReproductionInvestment, ref random));
    }

    private static float BlendValue(float a, float b, ref DeterministicRandom random)
    {
        var t = random.NextFloat();
        return a + (b - a) * t;
    }

    private static float MutateValue(float value, ref DeterministicRandom random, MutationSettings settings)
    {
        if (random.NextFloat() >= settings.Probability)
            return value;
        var delta = (random.NextFloat() * 2f - 1f) * settings.Strength;
        return Math.Clamp(value + delta, 0f, 1f);
    }
}

public sealed record LineageRecord(
    LineageId Id,
    LineageId? ParentId,
    GenomeId FounderGenomeId,
    long CreatedTick);

public sealed class LineageStore
{
    private readonly List<LineageRecord> _records = new();

    public int Count => _records.Count;
    public IReadOnlyList<LineageRecord> Records => _records;

    public LineageId CreateFounder(GenomeId founderGenomeId, long createdTick)
    {
        var id = new LineageId(_records.Count + 1);
        _records.Add(new LineageRecord(id, null, founderGenomeId, createdTick));
        return id;
    }

    public LineageId CreateChild(LineageId parentId, GenomeId founderGenomeId, long createdTick)
    {
        if (parentId.Value < 1 || parentId.Value > _records.Count)
            throw new KeyNotFoundException($"Unknown parent lineage {parentId.Value}.");
        var id = new LineageId(_records.Count + 1);
        _records.Add(new LineageRecord(id, parentId, founderGenomeId, createdTick));
        return id;
    }

    public LineageSnapshot CaptureSnapshot()
    {
        return new LineageSnapshot { Records = _records.ToArray() };
    }

    public static LineageStore Restore(LineageSnapshot snapshot)
    {
        var store = new LineageStore();
        foreach (var record in snapshot.Records ?? Array.Empty<LineageRecord>())
            store._records.Add(record);
        return store;
    }
}

public sealed class LineageSnapshot
{
    public LineageRecord[] Records { get; set; } = Array.Empty<LineageRecord>();
}
