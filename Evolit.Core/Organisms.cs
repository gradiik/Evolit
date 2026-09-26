using System;
using System.Collections.Generic;

namespace Evolit.Core;

[Flags]
public enum OrganismFlags : byte
{
    None = 0,
    Alive = 1 << 0
}

public readonly record struct OrganismState(
    OrganismId Id,
    CellId CellId,
    GenomeId GenomeId,
    LineageId LineageId,
    double AgeSeconds,
    float Energy,
    float Health,
    OrganismFlags Flags)
{
    public bool IsAlive => (Flags & OrganismFlags.Alive) != 0;
}

public sealed class OrganismStore
{
    private OrganismId[] _ids = new OrganismId[16];
    private CellId[] _cells = new CellId[16];
    private GenomeId[] _genomes = new GenomeId[16];
    private LineageId[] _lineages = new LineageId[16];
    private double[] _ageSeconds = new double[16];
    private float[] _energy = new float[16];
    private float[] _health = new float[16];
    private OrganismFlags[] _flags = new OrganismFlags[16];
    private readonly Dictionary<OrganismId, int> _indexById = new();
    private int _count;
    private long _nextId = 1;

    public int Count => _count;

    public OrganismId Create(
        CellId cellId,
        GenomeId genomeId,
        LineageId lineageId,
        float energy = 1f,
        float health = 1f)
    {
        EnsureCapacity(_count + 1);
        var id = new OrganismId(_nextId++);
        var index = _count++;
        _ids[index] = id;
        _cells[index] = cellId;
        _genomes[index] = genomeId;
        _lineages[index] = lineageId;
        _ageSeconds[index] = 0;
        _energy[index] = Math.Clamp(energy, 0f, 1f);
        _health[index] = Math.Clamp(health, 0f, 1f);
        _flags[index] = OrganismFlags.Alive;
        _indexById.Add(id, index);
        return id;
    }

    public bool TryGet(OrganismId id, out OrganismState state)
    {
        if (!_indexById.TryGetValue(id, out var index))
        {
            state = default;
            return false;
        }

        state = Read(index);
        return true;
    }

    public bool Remove(OrganismId id)
    {
        if (!_indexById.TryGetValue(id, out var index))
            return false;

        var last = _count - 1;
        _indexById.Remove(id);
        if (index != last)
        {
            _ids[index] = _ids[last];
            _cells[index] = _cells[last];
            _genomes[index] = _genomes[last];
            _lineages[index] = _lineages[last];
            _ageSeconds[index] = _ageSeconds[last];
            _energy[index] = _energy[last];
            _health[index] = _health[last];
            _flags[index] = _flags[last];
            _indexById[_ids[index]] = index;
        }

        _count--;
        return true;
    }

    public bool SetCell(OrganismId id, CellId cellId)
    {
        if (!_indexById.TryGetValue(id, out var index))
            return false;
        _cells[index] = cellId;
        return true;
    }

    public bool SetEnergyHealth(OrganismId id, float energy, float health)
    {
        if (!_indexById.TryGetValue(id, out var index))
            return false;
        _energy[index] = Math.Clamp(energy, 0f, 1f);
        _health[index] = Math.Clamp(health, 0f, 1f);
        return true;
    }

    internal void AdvanceFoundation(double fixedDeltaSeconds, GenomeStore genomes)
    {
        var dayFraction = fixedDeltaSeconds / 86400.0;
        for (var index = 0; index < _count; index++)
        {
            if ((_flags[index] & OrganismFlags.Alive) == 0)
                continue;

            _ageSeconds[index] += fixedDeltaSeconds;
            var phenotype = genomes.GetPhenotype(_genomes[index]);
            var energyDrain = (float)(fixedDeltaSeconds * 0.000015 * phenotype.MetabolicRate);
            _energy[index] = Math.Max(0f, _energy[index] - energyDrain);
            if (_energy[index] <= 0.0001f)
                _health[index] = Math.Max(0f, _health[index] - (float)(dayFraction * 0.05));
            if (_health[index] <= 0f)
                _flags[index] &= ~OrganismFlags.Alive;
        }
    }

    public OrganismStoreSnapshot CaptureSnapshot()
    {
        var snapshot = new OrganismStoreSnapshot
        {
            NextId = _nextId,
            Ids = new OrganismId[_count],
            Cells = new CellId[_count],
            Genomes = new GenomeId[_count],
            Lineages = new LineageId[_count],
            AgeSeconds = new double[_count],
            Energy = new float[_count],
            Health = new float[_count],
            Flags = new OrganismFlags[_count]
        };

        Array.Copy(_ids, snapshot.Ids, _count);
        Array.Copy(_cells, snapshot.Cells, _count);
        Array.Copy(_genomes, snapshot.Genomes, _count);
        Array.Copy(_lineages, snapshot.Lineages, _count);
        Array.Copy(_ageSeconds, snapshot.AgeSeconds, _count);
        Array.Copy(_energy, snapshot.Energy, _count);
        Array.Copy(_health, snapshot.Health, _count);
        Array.Copy(_flags, snapshot.Flags, _count);
        return snapshot;
    }

    public static OrganismStore Restore(OrganismStoreSnapshot snapshot)
    {
        var count = snapshot.Ids?.Length ?? 0;
        EnsureSameLength(count, snapshot.Cells, nameof(snapshot.Cells));
        EnsureSameLength(count, snapshot.Genomes, nameof(snapshot.Genomes));
        EnsureSameLength(count, snapshot.Lineages, nameof(snapshot.Lineages));
        EnsureSameLength(count, snapshot.AgeSeconds, nameof(snapshot.AgeSeconds));
        EnsureSameLength(count, snapshot.Energy, nameof(snapshot.Energy));
        EnsureSameLength(count, snapshot.Health, nameof(snapshot.Health));
        EnsureSameLength(count, snapshot.Flags, nameof(snapshot.Flags));

        var store = new OrganismStore();
        store.EnsureCapacity(count);
        store._count = count;
        store._nextId = Math.Max(1, snapshot.NextId);
        Array.Copy(snapshot.Ids!, store._ids, count);
        Array.Copy(snapshot.Cells!, store._cells, count);
        Array.Copy(snapshot.Genomes!, store._genomes, count);
        Array.Copy(snapshot.Lineages!, store._lineages, count);
        Array.Copy(snapshot.AgeSeconds!, store._ageSeconds, count);
        Array.Copy(snapshot.Energy!, store._energy, count);
        Array.Copy(snapshot.Health!, store._health, count);
        Array.Copy(snapshot.Flags!, store._flags, count);

        for (var index = 0; index < count; index++)
        {
            if (!store._indexById.TryAdd(store._ids[index], index))
                throw new InvalidOperationException($"Duplicate organism id {store._ids[index].Value} in snapshot.");
        }

        return store;
    }

    private OrganismState Read(int index)
    {
        return new OrganismState(
            _ids[index],
            _cells[index],
            _genomes[index],
            _lineages[index],
            _ageSeconds[index],
            _energy[index],
            _health[index],
            _flags[index]);
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _ids.Length)
            return;

        var next = Math.Max(required, _ids.Length * 2);
        Array.Resize(ref _ids, next);
        Array.Resize(ref _cells, next);
        Array.Resize(ref _genomes, next);
        Array.Resize(ref _lineages, next);
        Array.Resize(ref _ageSeconds, next);
        Array.Resize(ref _energy, next);
        Array.Resize(ref _health, next);
        Array.Resize(ref _flags, next);
    }

    private static void EnsureSameLength(int expected, Array? array, string name)
    {
        if (array is null || array.Length != expected)
            throw new InvalidOperationException($"Organism snapshot field {name} has invalid length.");
    }
}

public sealed class OrganismStoreSnapshot
{
    public long NextId { get; set; } = 1;
    public OrganismId[] Ids { get; set; } = Array.Empty<OrganismId>();
    public CellId[] Cells { get; set; } = Array.Empty<CellId>();
    public GenomeId[] Genomes { get; set; } = Array.Empty<GenomeId>();
    public LineageId[] Lineages { get; set; } = Array.Empty<LineageId>();
    public double[] AgeSeconds { get; set; } = Array.Empty<double>();
    public float[] Energy { get; set; } = Array.Empty<float>();
    public float[] Health { get; set; } = Array.Empty<float>();
    public OrganismFlags[] Flags { get; set; } = Array.Empty<OrganismFlags>();
}
