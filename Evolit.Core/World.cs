using System;
using System.Collections.Generic;

namespace Evolit.Core;

public sealed class WorldTopology
{
    private readonly CellId[] _cells;
    private readonly int[] _neighborOffsets;
    private readonly CellId[] _neighbors;
    private readonly Dictionary<CellId, int> _indexById;

    public WorldTopology(CellId[] cells, int[] neighborOffsets, CellId[] neighbors)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(neighborOffsets);
        ArgumentNullException.ThrowIfNull(neighbors);
        if (neighborOffsets.Length != cells.Length + 1)
            throw new ArgumentException("Neighbor offsets must contain Count + 1 entries.", nameof(neighborOffsets));
        if (neighborOffsets.Length > 0 && neighborOffsets[^1] != neighbors.Length)
            throw new ArgumentException("Neighbor offsets do not match neighbor storage.", nameof(neighborOffsets));

        _cells = cells;
        _neighborOffsets = neighborOffsets;
        _neighbors = neighbors;
        _indexById = new Dictionary<CellId, int>(cells.Length);
        for (var index = 0; index < cells.Length; index++)
        {
            if (!_indexById.TryAdd(cells[index], index))
                throw new ArgumentException($"Duplicate cell id {cells[index]}.", nameof(cells));
        }
    }

    public int Count => _cells.Length;
    public ReadOnlySpan<CellId> Cells => _cells;

    public CellId GetCellId(int index) => _cells[index];

    public bool TryGetIndex(CellId id, out int index) => _indexById.TryGetValue(id, out index);

    public ReadOnlySpan<CellId> GetNeighbors(CellId id)
    {
        if (!_indexById.TryGetValue(id, out var index))
            return ReadOnlySpan<CellId>.Empty;

        var start = _neighborOffsets[index];
        var length = _neighborOffsets[index + 1] - start;
        return _neighbors.AsSpan(start, length);
    }

    public WorldTopologySnapshot CaptureSnapshot()
    {
        return new WorldTopologySnapshot
        {
            Cells = (CellId[])_cells.Clone(),
            NeighborOffsets = (int[])_neighborOffsets.Clone(),
            Neighbors = (CellId[])_neighbors.Clone()
        };
    }

    public static WorldTopology Restore(WorldTopologySnapshot snapshot)
    {
        return new WorldTopology(
            snapshot.Cells ?? Array.Empty<CellId>(),
            snapshot.NeighborOffsets ?? Array.Empty<int>(),
            snapshot.Neighbors ?? Array.Empty<CellId>());
    }
}

public sealed class WorldTopologyBuilder
{
    private readonly List<CellId> _cells = new();
    private readonly List<CellId[]> _neighbors = new();
    private readonly HashSet<CellId> _seen = new();

    public void Add(CellId id, IReadOnlyList<CellId> neighbors)
    {
        if (!_seen.Add(id))
            throw new InvalidOperationException($"Cell {id} has already been added.");

        _cells.Add(id);
        var copy = new CellId[neighbors.Count];
        for (var index = 0; index < neighbors.Count; index++)
            copy[index] = neighbors[index];
        _neighbors.Add(copy);
    }

    public WorldTopology Build()
    {
        var offsets = new int[_cells.Count + 1];
        var total = 0;
        for (var index = 0; index < _neighbors.Count; index++)
        {
            offsets[index] = total;
            total += _neighbors[index].Length;
        }
        offsets[^1] = total;

        var flat = new CellId[total];
        var cursor = 0;
        foreach (var set in _neighbors)
        {
            set.CopyTo(flat, cursor);
            cursor += set.Length;
        }

        return new WorldTopology(_cells.ToArray(), offsets, flat);
    }
}

public sealed class WorldTopologySnapshot
{
    public CellId[] Cells { get; set; } = Array.Empty<CellId>();
    public int[] NeighborOffsets { get; set; } = Array.Empty<int>();
    public CellId[] Neighbors { get; set; } = Array.Empty<CellId>();
}

public enum SubstrateKind : byte
{
    Unknown = 0,
    BareRock = 1,
    Basalt = 2,
    VolcanicAsh = 3,
    Sand = 4,
    MineralRegolith = 5,
    Sediment = 6,
    IceSnow = 7
}

public readonly record struct EnvironmentCellState(
    float ElevationMeters,
    float WaterDepthMeters,
    float TemperatureCelsius,
    float Humidity,
    float PressureKPa,
    float LightAvailability,
    float MineralPotential,
    float NutrientPotential,
    float OrganicMatter,
    float SubstrateDevelopment,
    float GeothermalPotential,
    SubstrateKind Substrate);

public sealed class EnvironmentStore
{
    private readonly WorldTopology _topology;
    private readonly float[] _elevationMeters;
    private readonly float[] _waterDepthMeters;
    private readonly float[] _temperatureCelsius;
    private readonly float[] _humidity;
    private readonly float[] _pressureKPa;
    private readonly float[] _lightAvailability;
    private readonly float[] _mineralPotential;
    private readonly float[] _nutrientPotential;
    private readonly float[] _organicMatter;
    private readonly float[] _substrateDevelopment;
    private readonly float[] _geothermalPotential;
    private readonly SubstrateKind[] _substrate;

    public EnvironmentStore(WorldTopology topology)
    {
        _topology = topology ?? throw new ArgumentNullException(nameof(topology));
        var count = topology.Count;
        _elevationMeters = new float[count];
        _waterDepthMeters = new float[count];
        _temperatureCelsius = new float[count];
        _humidity = new float[count];
        _pressureKPa = new float[count];
        _lightAvailability = new float[count];
        _mineralPotential = new float[count];
        _nutrientPotential = new float[count];
        _organicMatter = new float[count];
        _substrateDevelopment = new float[count];
        _geothermalPotential = new float[count];
        _substrate = new SubstrateKind[count];
    }

    public int Count => _topology.Count;

    public void SetInitial(CellId id, EnvironmentCellState state)
    {
        if (!_topology.TryGetIndex(id, out var index))
            throw new KeyNotFoundException($"Unknown cell {id}.");

        Write(index, state);
    }

    public EnvironmentCellState Get(CellId id)
    {
        if (!_topology.TryGetIndex(id, out var index))
            throw new KeyNotFoundException($"Unknown cell {id}.");

        return Read(index);
    }

    internal void FoundationUpdate(float rate)
    {
        var boundedRate = Math.Clamp(rate, 0f, 1f);
        for (var index = 0; index < Count; index++)
        {
            _humidity[index] = Math.Clamp(_humidity[index], 0f, 1f);
            _lightAvailability[index] = Math.Clamp(_lightAvailability[index], 0f, 1f);
            _organicMatter[index] = Math.Max(0f, _organicMatter[index] * (1f - 0.001f * boundedRate));
            _nutrientPotential[index] = Math.Clamp(
                _nutrientPotential[index] + _organicMatter[index] * 0.0002f * boundedRate,
                0f,
                1f);
            _substrateDevelopment[index] = Math.Clamp(_substrateDevelopment[index], 0f, 1f);
            _geothermalPotential[index] = Math.Clamp(_geothermalPotential[index], 0f, 1f);
        }
    }

    public EnvironmentSnapshot CaptureSnapshot()
    {
        return new EnvironmentSnapshot
        {
            ElevationMeters = (float[])_elevationMeters.Clone(),
            WaterDepthMeters = (float[])_waterDepthMeters.Clone(),
            TemperatureCelsius = (float[])_temperatureCelsius.Clone(),
            Humidity = (float[])_humidity.Clone(),
            PressureKPa = (float[])_pressureKPa.Clone(),
            LightAvailability = (float[])_lightAvailability.Clone(),
            MineralPotential = (float[])_mineralPotential.Clone(),
            NutrientPotential = (float[])_nutrientPotential.Clone(),
            OrganicMatter = (float[])_organicMatter.Clone(),
            SubstrateDevelopment = (float[])_substrateDevelopment.Clone(),
            GeothermalPotential = (float[])_geothermalPotential.Clone(),
            Substrate = (SubstrateKind[])_substrate.Clone()
        };
    }

    public static EnvironmentStore Restore(WorldTopology topology, EnvironmentSnapshot snapshot)
    {
        var store = new EnvironmentStore(topology);
        store.CopyFrom(snapshot);
        return store;
    }

    private void CopyFrom(EnvironmentSnapshot snapshot)
    {
        EnsureLength(snapshot.ElevationMeters, nameof(snapshot.ElevationMeters));
        EnsureLength(snapshot.WaterDepthMeters, nameof(snapshot.WaterDepthMeters));
        EnsureLength(snapshot.TemperatureCelsius, nameof(snapshot.TemperatureCelsius));
        EnsureLength(snapshot.Humidity, nameof(snapshot.Humidity));
        EnsureLength(snapshot.PressureKPa, nameof(snapshot.PressureKPa));
        EnsureLength(snapshot.LightAvailability, nameof(snapshot.LightAvailability));
        EnsureLength(snapshot.MineralPotential, nameof(snapshot.MineralPotential));
        EnsureLength(snapshot.NutrientPotential, nameof(snapshot.NutrientPotential));
        EnsureLength(snapshot.OrganicMatter, nameof(snapshot.OrganicMatter));
        EnsureLength(snapshot.SubstrateDevelopment, nameof(snapshot.SubstrateDevelopment));
        EnsureLength(snapshot.GeothermalPotential, nameof(snapshot.GeothermalPotential));
        EnsureLength(snapshot.Substrate, nameof(snapshot.Substrate));

        snapshot.ElevationMeters.CopyTo(_elevationMeters, 0);
        snapshot.WaterDepthMeters.CopyTo(_waterDepthMeters, 0);
        snapshot.TemperatureCelsius.CopyTo(_temperatureCelsius, 0);
        snapshot.Humidity.CopyTo(_humidity, 0);
        snapshot.PressureKPa.CopyTo(_pressureKPa, 0);
        snapshot.LightAvailability.CopyTo(_lightAvailability, 0);
        snapshot.MineralPotential.CopyTo(_mineralPotential, 0);
        snapshot.NutrientPotential.CopyTo(_nutrientPotential, 0);
        snapshot.OrganicMatter.CopyTo(_organicMatter, 0);
        snapshot.SubstrateDevelopment.CopyTo(_substrateDevelopment, 0);
        snapshot.GeothermalPotential.CopyTo(_geothermalPotential, 0);
        snapshot.Substrate.CopyTo(_substrate, 0);
    }

    private void EnsureLength(Array array, string name)
    {
        if (array.Length != Count)
            throw new InvalidOperationException($"Environment snapshot field {name} has invalid length {array.Length}; expected {Count}.");
    }

    private EnvironmentCellState Read(int index)
    {
        return new EnvironmentCellState(
            _elevationMeters[index],
            _waterDepthMeters[index],
            _temperatureCelsius[index],
            _humidity[index],
            _pressureKPa[index],
            _lightAvailability[index],
            _mineralPotential[index],
            _nutrientPotential[index],
            _organicMatter[index],
            _substrateDevelopment[index],
            _geothermalPotential[index],
            _substrate[index]);
    }

    private void Write(int index, EnvironmentCellState state)
    {
        _elevationMeters[index] = state.ElevationMeters;
        _waterDepthMeters[index] = Math.Max(0f, state.WaterDepthMeters);
        _temperatureCelsius[index] = state.TemperatureCelsius;
        _humidity[index] = Math.Clamp(state.Humidity, 0f, 1f);
        _pressureKPa[index] = Math.Max(0f, state.PressureKPa);
        _lightAvailability[index] = Math.Clamp(state.LightAvailability, 0f, 1f);
        _mineralPotential[index] = Math.Clamp(state.MineralPotential, 0f, 1f);
        _nutrientPotential[index] = Math.Clamp(state.NutrientPotential, 0f, 1f);
        _organicMatter[index] = Math.Max(0f, state.OrganicMatter);
        _substrateDevelopment[index] = Math.Clamp(state.SubstrateDevelopment, 0f, 1f);
        _geothermalPotential[index] = Math.Clamp(state.GeothermalPotential, 0f, 1f);
        _substrate[index] = state.Substrate;
    }
}

public sealed class EnvironmentSnapshot
{
    public float[] ElevationMeters { get; set; } = Array.Empty<float>();
    public float[] WaterDepthMeters { get; set; } = Array.Empty<float>();
    public float[] TemperatureCelsius { get; set; } = Array.Empty<float>();
    public float[] Humidity { get; set; } = Array.Empty<float>();
    public float[] PressureKPa { get; set; } = Array.Empty<float>();
    public float[] LightAvailability { get; set; } = Array.Empty<float>();
    public float[] MineralPotential { get; set; } = Array.Empty<float>();
    public float[] NutrientPotential { get; set; } = Array.Empty<float>();
    public float[] OrganicMatter { get; set; } = Array.Empty<float>();
    public float[] SubstrateDevelopment { get; set; } = Array.Empty<float>();
    public float[] GeothermalPotential { get; set; } = Array.Empty<float>();
    public SubstrateKind[] Substrate { get; set; } = Array.Empty<SubstrateKind>();
}
