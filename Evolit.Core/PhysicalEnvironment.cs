using System;

namespace Evolit.Core;

public enum EnvironmentRegion : byte
{
    Unknown = 0,
    DeepWater,
    ShallowWater,
    Wetland,
    Ice,
    BarrenRock,
    Arid,
    Cold,
    TemperateDry,
    TemperateHumid,
    WarmHumid
}

public readonly record struct PhysicalEnvironmentState(
    float WindX,
    float WindY,
    float Precipitation,
    float Evaporation,
    float Runoff,
    float WaterAvailability,
    EnvironmentRegion Region);

public sealed partial class EnvironmentStore
{
    private float[]? _windX;
    private float[]? _windY;
    private float[]? _precipitation;
    private float[]? _evaporation;
    private float[]? _runoff;
    private float[]? _waterAvailability;
    private float[]? _temperatureScratch;
    private float[]? _humidityScratch;
    private float[]? _waterScratch;
    private float[]? _waterDelta;
    private float[]? _pressureScratch;
    private float[]? _nutrientScratch;
    private float[]? _baseClimateTemperature;
    private float[]? _targetPressure;

    public PhysicalEnvironmentState GetPhysical(CellId id)
    {
        if (!_topology.TryGetIndex(id, out var index))
            throw new ArgumentOutOfRangeException(nameof(id));
        EnsurePhysicalStorage();
        return new PhysicalEnvironmentState(
            _windX![index], _windY![index], _precipitation![index], _evaporation![index],
            _runoff![index], _waterAvailability![index], Classify(index));
    }

    internal void UpdateClimate(double simulationSeconds, float rate)
    {
        EnsurePhysicalStorage();
        var blend = Math.Clamp(0.025f * rate, 0f, 0.25f);
        var dayPhase = (float)(simulationSeconds % 86400.0 / 86400.0 * Math.PI * 2.0);
        var dayWave = MathF.Sin(dayPhase);

        for (var i = 0; i < Count; i++)
        {
            var waterModeration = Math.Clamp(_waterAvailability![i], 0f, 1f);
            var target = _baseClimateTemperature![i] + dayWave * (3.5f - waterModeration * 2.5f);
            var neighbors = _topology.GetNeighborIndices(i);
            if (neighbors.Length > 0)
            {
                var sum = 0f;
                for (var n = 0; n < neighbors.Length; n++)
                    sum += _temperatureCelsius[neighbors[n]];
                target = target * 0.82f + (sum / neighbors.Length) * 0.18f;
            }

            _temperatureScratch![i] = Math.Clamp(
                _temperatureCelsius[i] + (target - _temperatureCelsius[i]) * blend,
                -80f,
                65f);
        }

        SwapInto(_temperatureScratch!, _temperatureCelsius);
    }

    internal void UpdateAtmosphere(float rate)
    {
        EnsurePhysicalStorage();
        var blend = Math.Clamp(0.04f * rate, 0f, 0.3f);

        for (var i = 0; i < Count; i++)
        {
            _pressureScratch![i] = Math.Clamp(
                _pressureKPa[i] + (_targetPressure![i] - _pressureKPa[i]) * blend,
                20f,
                115f);

            var gx = 0f;
            var gy = 0f;
            var id = _topology.GetCellId(i);
            var neighbors = _topology.GetNeighborIndices(i);
            for (var n = 0; n < neighbors.Length; n++)
            {
                var ni = neighbors[n];
                var neighborId = _topology.GetCellId(ni);
                var dp = _pressureKPa[i] - _pressureKPa[ni];
                gx += dp * (neighborId.Q - id.Q);
                gy += dp * (neighborId.R - id.R);
            }

            var scale = neighbors.Length == 0 ? 0f : 0.18f / neighbors.Length;
            _windX![i] = Math.Clamp(gx * scale, -1f, 1f);
            _windY![i] = Math.Clamp(gy * scale, -1f, 1f);
        }

        SwapInto(_pressureScratch!, _pressureKPa);
    }

    internal void UpdateHydrology(float rate)
    {
        EnsurePhysicalStorage();
        Array.Clear(_waterDelta!, 0, Count);
        for (var i = 0; i < Count; i++)
        {
            var water = _waterDepthMeters[i];
            var temperature = _temperatureCelsius[i];
            var evaporation = water > 0f ? Math.Min(water, Math.Max(0f, temperature + 5f) * 0.00002f * rate) : 0f;
            var precipitation = _humidity[i] > 0.72f
                ? (_humidity[i] - 0.72f) * Math.Clamp((temperature + 35f) / 55f, 0f, 1f) * 0.003f * rate
                : 0f;
            _evaporation![i] = evaporation;
            _precipitation![i] = precipitation;
            _waterDelta![i] += precipitation - evaporation;

            var neighbors = _topology.GetNeighborIndices(i);
            var best = -1;
            var sourceSurface = _elevationMeters[i] + water;
            var bestSurface = sourceSurface;
            for (var n = 0; n < neighbors.Length; n++)
            {
                var ni = neighbors[n];
                var surface = _elevationMeters[ni] + _waterDepthMeters[ni];
                if (surface < bestSurface)
                {
                    bestSurface = surface;
                    best = ni;
                }
            }

            var available = Math.Max(0f, water + precipitation - evaporation);
            var flow = 0f;
            if (best >= 0 && available > 0f)
            {
                var head = sourceSurface - bestSurface;
                flow = Math.Min(available, Math.Max(0f, head) * 0.015f * rate);
                _waterDelta[i] -= flow;
                _waterDelta[best] += flow;
            }
            _runoff![i] = flow;
        }

        for (var i = 0; i < Count; i++)
        {
            _waterScratch![i] = Math.Max(0f, _waterDepthMeters[i] + _waterDelta![i]);
            _waterAvailability![i] = Math.Clamp(_waterScratch[i] > 0f ? 1f : _humidity[i] * 0.72f + _precipitation![i] * 8f, 0f, 1f);
        }
        SwapInto(_waterScratch!, _waterDepthMeters);
    }

    internal void UpdateHumidity(float rate)
    {
        EnsurePhysicalStorage();
        var blend = Math.Clamp(0.035f * rate, 0f, 0.25f);
        for (var i = 0; i < Count; i++)
        {
            var neighborHumidity = 0f;
            var neighbors = _topology.GetNeighborIndices(i);
            for (var n = 0; n < neighbors.Length; n++)
                neighborHumidity += _humidity[neighbors[n]];
            if (neighbors.Length > 0)
                neighborHumidity /= neighbors.Length;

            var waterSource = _waterAvailability![i] * 0.72f + Math.Min(0.2f, _evaporation![i] * 30f);
            var target = Math.Clamp(waterSource + neighborHumidity * 0.28f - _precipitation![i] * 0.8f, 0f, 1f);
            _humidityScratch![i] = Math.Clamp(_humidity[i] + (target - _humidity[i]) * blend, 0f, 1f);
        }
        SwapInto(_humidityScratch!, _humidity);
    }

    internal void UpdateResources(float rate)
    {
        EnsurePhysicalStorage();
        var blend = Math.Clamp(0.01f * rate, 0f, 0.15f);
        for (var i = 0; i < Count; i++)
        {
            var weathering = _waterAvailability![i] * 0.45f + Math.Clamp((_temperatureCelsius[i] + 20f) / 60f, 0f, 1f) * 0.15f;
            var targetNutrient = Math.Clamp(_mineralPotential[i] * 0.55f + weathering * 0.25f + _organicMatter[i] * 0.2f, 0f, 1f);
            _nutrientScratch![i] = Math.Clamp(_nutrientPotential[i] + (targetNutrient - _nutrientPotential[i]) * blend, 0f, 1f);
            _organicMatter[i] = Math.Max(0f, _organicMatter[i] * (1f - 0.0005f * rate));
            _substrateDevelopment[i] = Math.Clamp(
                _substrateDevelopment[i] + (_waterAvailability[i] * _mineralPotential[i] + _organicMatter[i]) * 0.00005f * rate,
                0f, 1f);
        }
        SwapInto(_nutrientScratch!, _nutrientPotential);
    }

    internal void UpdateLight(double simulationSeconds)
    {
        var phase = (float)(simulationSeconds % 86400.0 / 86400.0 * Math.PI * 2.0);
        var daylight = Math.Clamp(0.5f + 0.5f * MathF.Sin(phase), 0.05f, 1f);
        for (var i = 0; i < Count; i++)
            _lightAvailability[i] = Math.Clamp(daylight * (1f - _humidity[i] * 0.18f) * (_waterDepthMeters[i] > 100f ? 0.55f : 1f), 0f, 1f);
    }

    public double MeasureChange(EnvironmentSnapshot before)
    {
        var total = 0.0;
        for (var i = 0; i < Count; i++)
        {
            total += Math.Abs(_temperatureCelsius[i] - before.TemperatureCelsius[i]);
            total += Math.Abs(_humidity[i] - before.Humidity[i]) * 20.0;
            total += Math.Abs(_waterDepthMeters[i] - before.WaterDepthMeters[i]) * 0.01;
        }
        return Count == 0 ? 0 : total / Count;
    }

    internal void CapturePhysical(EnvironmentSnapshot snapshot)
    {
        EnsurePhysicalStorage();
        snapshot.WindX = (float[])_windX!.Clone();
        snapshot.WindY = (float[])_windY!.Clone();
        snapshot.Precipitation = (float[])_precipitation!.Clone();
        snapshot.Evaporation = (float[])_evaporation!.Clone();
        snapshot.Runoff = (float[])_runoff!.Clone();
        snapshot.WaterAvailability = (float[])_waterAvailability!.Clone();
    }

    internal void RestorePhysical(EnvironmentSnapshot snapshot)
    {
        EnsurePhysicalStorage();
        CopyOptional(snapshot.WindX, _windX!);
        CopyOptional(snapshot.WindY, _windY!);
        CopyOptional(snapshot.Precipitation, _precipitation!);
        CopyOptional(snapshot.Evaporation, _evaporation!);
        CopyOptional(snapshot.Runoff, _runoff!);
        CopyOptional(snapshot.WaterAvailability, _waterAvailability!);
        for (var i = 0; i < Count; i++)
            if (snapshot.WaterAvailability.Length == 0)
                _waterAvailability![i] = _waterDepthMeters[i] > 0 ? 1f : _humidity[i] * 0.72f;
    }

    private EnvironmentRegion Classify(int i)
    {
        if (_waterDepthMeters[i] > 120f) return EnvironmentRegion.DeepWater;
        if (_waterDepthMeters[i] > 2f) return EnvironmentRegion.ShallowWater;
        if (_waterAvailability![i] > 0.82f && _humidity[i] > 0.75f) return EnvironmentRegion.Wetland;
        if (_temperatureCelsius[i] < -8f) return EnvironmentRegion.Ice;
        if ((_substrate[i] is SubstrateKind.BareRock or SubstrateKind.Basalt) && _substrateDevelopment[i] < 0.08f) return EnvironmentRegion.BarrenRock;
        if (_humidity[i] < 0.22f) return EnvironmentRegion.Arid;
        if (_temperatureCelsius[i] < 5f) return EnvironmentRegion.Cold;
        if (_temperatureCelsius[i] > 23f && _humidity[i] > 0.68f) return EnvironmentRegion.WarmHumid;
        return _humidity[i] > 0.52f ? EnvironmentRegion.TemperateHumid : EnvironmentRegion.TemperateDry;
    }

    private void EnsurePhysicalStorage()
    {
        if (_windX is not null) return;
        _windX = new float[Count]; _windY = new float[Count];
        _precipitation = new float[Count]; _evaporation = new float[Count]; _runoff = new float[Count];
        _waterAvailability = new float[Count];
        _temperatureScratch = new float[Count]; _humidityScratch = new float[Count];
        _waterScratch = new float[Count]; _waterDelta = new float[Count]; _pressureScratch = new float[Count]; _nutrientScratch = new float[Count];
        _baseClimateTemperature = new float[Count];
        _targetPressure = new float[Count];

        var maxAbsR = 1;
        for (var i = 0; i < Count; i++)
            maxAbsR = Math.Max(maxAbsR, Math.Abs(_topology.GetCellId(i).R));

        for (var i = 0; i < Count; i++)
        {
            _waterAvailability[i] = _waterDepthMeters[i] > 0 ? 1f : _humidity[i] * 0.72f;

            var id = _topology.GetCellId(i);
            var latitude = Math.Clamp(Math.Abs(id.R) / (float)maxAbsR, 0f, 1f);
            var elevationCooling = Math.Max(0f, _elevationMeters[i]) * 0.0062f;
            _baseClimateTemperature[i] = 29f - latitude * 31f - elevationCooling;
            _targetPressure[i] = Math.Max(
                20f,
                101.325f * MathF.Exp(-Math.Max(-500f, _elevationMeters[i]) / 8434f));
        }
    }

    private static void SwapInto(float[] source, float[] target) => Array.Copy(source, target, source.Length);
    private static void CopyOptional(float[] source, float[] target)
    {
        if (source.Length == 0) return;
        if (source.Length != target.Length) throw new InvalidOperationException("Physical environment snapshot length mismatch.");
        Array.Copy(source, target, source.Length);
    }
}

public sealed partial class EnvironmentSnapshot
{
    public float[] WindX { get; set; } = Array.Empty<float>();
    public float[] WindY { get; set; } = Array.Empty<float>();
    public float[] Precipitation { get; set; } = Array.Empty<float>();
    public float[] Evaporation { get; set; } = Array.Empty<float>();
    public float[] Runoff { get; set; } = Array.Empty<float>();
    public float[] WaterAvailability { get; set; } = Array.Empty<float>();
}
