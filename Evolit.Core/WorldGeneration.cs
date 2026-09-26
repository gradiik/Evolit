using System;

namespace Evolit.Core;

public enum WorldLandAmount : byte { Low, Normal, High }
public enum WorldClimate : byte { Cold, Temperate, Warm }
public enum GeologicalActivity : byte { Calm, Normal, Active }

public static class WorldGenerationScale
{
    public const int SmallRadius = 56;
    public const int MediumRadius = 76;
    public const int LargeRadius = 98;

    public const int LegacySmallRadius = 28;
    public const int LegacyMediumRadius = 38;
    public const int LegacyLargeRadius = 49;
}


public readonly record struct WorldGenerationSettings(
    string Seed,
    int Radius,
    WorldLandAmount LandAmount = WorldLandAmount.Normal,
    WorldClimate Climate = WorldClimate.Temperate,
    GeologicalActivity Geology = GeologicalActivity.Normal);

public readonly record struct GeneratedWorldCell(
    CellId Id,
    float ElevationMeters,
    float WaterDepthMeters,
    float TemperatureCelsius,
    float Humidity,
    float PressureKPa,
    float MineralPotential,
    float NutrientPotential,
    float GeothermalPotential,
    SubstrateKind Substrate,
    int DrainageTarget,
    float FlowAccumulation,
    float Slope,
    bool IsRiver,
    bool IsLake,
    int ProvinceId,
    float Continentalness,
    float TectonicUplift,
    int CoastDistance,
    int BasinId,
    int RiverLength,
    float RiverWidth);


public readonly record struct WorldGenerationQuality(
    int ContinentCount,
    int SecondLargestContinentCells,
    int TinyIslandCount,
    int InlandWaterComponents,
    int CoastlineEdges,
    float CoastlineComplexity,
    int MountainRangeCount,
    int RiverCount,
    int RiverTotalLength,
    int LongestRiver,
    int TributaryCount,
    int LakeCount,
    int LargestLakeCells,
    int DrainageBasinCount,
    float BoundaryOceanRatio,
    float MeanSlope)
{
    public static WorldGenerationQuality Empty => new();
}

public readonly record struct WorldGenerationMetrics(
    double TopologyMs,
    double MacroElevationMs,
    double CoastBathymetryMs,
    double GeologyMs,
    double HydrologyMs,
    double ClimateMs,
    double ResourcesMs,
    double EnvironmentBuildMs,
    double TotalMs);

public sealed class GeneratedWorld
{
    public required WorldGenerationSettings Settings { get; init; }
    public required WorldTopology Topology { get; init; }
    public required GeneratedWorldCell[] Cells { get; init; }
    public required EnvironmentStore Environment { get; init; }
    public required WorldGenerationSummary Summary { get; init; }
    public WorldGenerationQuality Quality { get; init; } = WorldGenerationQuality.Empty;
    public required WorldGenerationMetrics Metrics { get; init; }
}

public readonly record struct WorldGenerationSummary(
    int Cells, float LandRatio, int LandComponents, int LargestContinentCells,
    int IslandCount, int MountainCells, int RiverCells, int LakeCells,
    float MinElevationMeters, float MaxElevationMeters, float MaxWaterDepthMeters,
    float MinTemperatureCelsius, float MaxTemperatureCelsius,
    float MinHumidity, float MaxHumidity);

public static class ProceduralWorldGenerator
{
    public static GeneratedWorld Generate(WorldGenerationSettings settings)
    {
        if (settings.Radius < 4)
            throw new ArgumentOutOfRangeException(nameof(settings.Radius));

        var baseSeed = SeedMixer.FromString(settings.Seed ?? string.Empty);
        GeneratedWorld? best = null;
        var bestScore = double.NegativeInfinity;

        const int maxAttempts = 2;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var attemptSeed = attempt == 0
                ? baseSeed
                : SeedMixer.Combine(baseSeed, 9_000UL + (ulong)attempt);
            var candidate = GenerateCandidate(settings, attemptSeed);
            ValidatePhysical(candidate);

            var score = ScoreQuality(candidate, out var accepted);
            if (best is null || score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }

            if (accepted)
                return candidate;
        }

        // Visual-quality constraints are not allowed to make New Game fail.
        // Headless verification remains strict and will report seeds that need tuning.
        return best ?? throw new InvalidOperationException("World generation produced no candidate.");
    }

    private static GeneratedWorld GenerateCandidate(WorldGenerationSettings settings, ulong seed)
    {
        return WorldGeneration009Pipeline.Generate(settings, seed);
    }

    private static void ValidatePhysical(GeneratedWorld world)
    {
        var summary = world.Summary;
        if (summary.Cells <= 0)
            throw new InvalidOperationException("World generation produced no cells.");
        if (summary.LandRatio <= 0f || summary.LandRatio >= 1f)
            throw new InvalidOperationException("Generated world must contain both land and water.");
        if (summary.LargestContinentCells <= 0 || summary.LargestContinentCells >= summary.Cells)
            throw new InvalidOperationException("Generated world must contain coherent land and water.");

        for (var i = 0; i < world.Cells.Length; i++)
        {
            var cell = world.Cells[i];
            if (!float.IsFinite(cell.ElevationMeters) ||
                !float.IsFinite(cell.WaterDepthMeters) || cell.WaterDepthMeters < 0f ||
                !float.IsFinite(cell.TemperatureCelsius) ||
                !float.IsFinite(cell.Humidity) || cell.Humidity is < 0f or > 1f ||
                !float.IsFinite(cell.PressureKPa) || cell.PressureKPa <= 0f ||
                cell.MineralPotential is < 0f or > 1f ||
                cell.NutrientPotential is < 0f or > 1f ||
                cell.GeothermalPotential is < 0f or > 1f ||
                cell.Substrate == SubstrateKind.Unknown)
                throw new InvalidOperationException($"Generated cell {cell.Id} violates physical bounds.");

            if (cell.DrainageTarget >= 0)
            {
                if (cell.DrainageTarget >= world.Cells.Length)
                    throw new InvalidOperationException($"Generated cell {cell.Id} has an invalid drainage target.");

                var target = world.Cells[cell.DrainageTarget];
                var sourceSurface = cell.ElevationMeters + (cell.IsLake ? cell.WaterDepthMeters : 0f);
                var targetSurface = target.ElevationMeters + (target.IsLake ? target.WaterDepthMeters : 0f);
                if (targetSurface > sourceSurface + 0.1f)
                    throw new InvalidOperationException($"Generated drainage surface flows uphill from {cell.Id}.");
            }

            if (cell.IsLake && cell.ElevationMeters < 0f)
                throw new InvalidOperationException($"Generated lake {cell.Id} is below ocean level.");
        }
    }

    private static double ScoreQuality(GeneratedWorld world, out bool accepted)
    {
        var summary = world.Summary;
        var quality = world.Quality;
        var landCells = Math.Max(1, (int)MathF.Round(summary.Cells * summary.LandRatio));
        var largestShare = summary.LargestContinentCells / (double)landCells;
        var secondShare = quality.SecondLargestContinentCells / (double)landCells;
        var targetLand = world.Settings.LandAmount switch
        {
            WorldLandAmount.Low => 0.28,
            WorldLandAmount.High => 0.52,
            _ => 0.40
        };

        accepted =
            summary.LandRatio is >= 0.18f and <= 0.72f &&
            quality.ContinentCount is >= 2 and <= 5 &&
            largestShare is >= 0.20 and <= 0.84 &&
            secondShare >= 0.045 &&
            quality.TinyIslandCount <= Math.Max(24, summary.Cells / 450) &&
            quality.BoundaryOceanRatio >= 0.90f &&
            quality.MountainRangeCount > 0 &&
            quality.LongestRiver >= 5;

        var continentScore = quality.ContinentCount switch
        {
            2 or 3 or 4 => 2.0,
            1 or 5 => 1.0,
            _ => 0.0
        };

        return
            continentScore +
            Math.Min(1.2, secondShare * 4.0) +
            Math.Min(1.2, quality.CoastlineComplexity * 0.08) +
            Math.Min(1.0, quality.MountainRangeCount * 0.15) +
            Math.Min(1.0, quality.LongestRiver * 0.025) -
            Math.Abs(summary.LandRatio - targetLand) * 4.0 -
            Math.Max(0.0, largestShare - 0.82) * 5.0 -
            Math.Min(1.5, quality.TinyIslandCount / 20.0) -
            Math.Max(0.0, 0.92 - quality.BoundaryOceanRatio) * 6.0;
    }

}
