using System;
using System.Diagnostics;

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
    float RiverWidth,
    int StreamOrder,
    int UpstreamBranches,
    int RiverDirection);


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
    float MeanSlope,
    int LongestRiverStraightRun,
    float MeanRiverStraightRun,
    float LongRiverStraightFraction,
    int LongestCoastAxisRun,
    float MeanCoastAxisRun,
    float LongCoastAxisFraction,
    float RockyLandRatio,
    float MountainLandRatio,
    float PlainLandRatio,
    int InlandSeaCount,
    int LargestInlandSeaCells)
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
    public EnvironmentStore Environment { get; internal set; } = null!;
    public required WorldGenerationSummary Summary { get; init; }
    public WorldGenerationQuality Quality { get; init; } = WorldGenerationQuality.Empty;
    public WorldGenerationMetrics Metrics { get; internal set; }
    public int CandidateAttempt { get; internal set; }
    public bool CandidateAccepted { get; internal set; }
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

        var generationStart = Stopwatch.GetTimestamp();
        var baseSeed = SeedMixer.FromString(settings.Seed ?? string.Empty);
        var bestScore = double.NegativeInfinity;
        var bestSeed = baseSeed;
        var bestAttempt = 1;
        var hasBest = false;

        const int maxAttempts = 12;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var attemptSeed = attempt == 0
                ? baseSeed
                : SeedMixer.Combine(baseSeed, 9_000UL + (ulong)attempt);

            // Keep rejected candidate lifetime inside this iteration. We retain
            // only its seed/score instead of a second complete GeneratedWorld,
            // avoiding the previous best+current large-world memory overlap.
            var candidate = GenerateCandidate(settings, attemptSeed);
            ValidatePhysical(candidate);

            var score = ScoreQuality(candidate, out var accepted);
            if (!hasBest || score > bestScore)
            {
                bestScore = score;
                bestSeed = attemptSeed;
                bestAttempt = attempt + 1;
                hasBest = true;
            }

            if (!accepted)
                continue;

            candidate.CandidateAttempt = attempt + 1;
            candidate.CandidateAccepted = true;
            WorldGeneration009Pipeline.MaterializeEnvironment(candidate);
            ApplySelectionElapsed(candidate, generationStart);
            return candidate;
        }

        // Aesthetic quality never makes New Game fail. Re-generate only the best
        // deterministic attempt after all rejected candidates have become dead,
        // rather than retaining a complete best world during candidate search.
        if (!hasBest)
            throw new InvalidOperationException("World generation produced no candidate.");

        var best = GenerateCandidate(settings, bestSeed);
        ValidatePhysical(best);
        best.CandidateAttempt = bestAttempt;
        best.CandidateAccepted = false;
        WorldGeneration009Pipeline.MaterializeEnvironment(best);
        ApplySelectionElapsed(best, generationStart);
        return best;
    }

    private static void ApplySelectionElapsed(GeneratedWorld world, long started)
    {
        var m = world.Metrics;
        world.Metrics = new WorldGenerationMetrics(
            m.TopologyMs,
            m.MacroElevationMs,
            m.CoastBathymetryMs,
            m.GeologyMs,
            m.HydrologyMs,
            m.ClimateMs,
            m.ResourcesMs,
            m.EnvironmentBuildMs,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
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

            if (cell.ElevationMeters >= 0f && cell.DrainageTarget < 0)
                throw new InvalidOperationException($"Generated land cell {cell.Id} has no drainage outlet.");

            if (cell.DrainageTarget >= 0 &&
                world.Cells[cell.DrainageTarget].FlowAccumulation + 0.001f < cell.FlowAccumulation)
            {
                throw new InvalidOperationException(
                    $"Generated flow accumulation decreases downstream from {cell.Id}.");
            }

            if (cell.StreamOrder < 0 ||
                cell.UpstreamBranches < 0 ||
                cell.RiverDirection is < -1 or > 5)
            {
                throw new InvalidOperationException($"Generated river metadata is invalid in {cell.Id}.");
            }
        }

        ValidateDrainageAcyclic(world);
    }

    private static void ValidateDrainageAcyclic(GeneratedWorld world)
    {
        var state = new byte[world.Cells.Length];
        var stack = new int[world.Cells.Length];

        for (var start = 0; start < world.Cells.Length; start++)
        {
            if (state[start] != 0 || world.Cells[start].ElevationMeters < 0f)
                continue;

            var depth = 0;
            var current = start;

            while (current >= 0 && world.Cells[current].ElevationMeters >= 0f)
            {
                if (state[current] == 2)
                    break;
                if (state[current] == 1)
                    throw new InvalidOperationException(
                        $"Generated drainage contains a cycle at {world.Cells[current].Id}.");

                state[current] = 1;
                stack[depth++] = current;
                current = world.Cells[current].DrainageTarget;

                if (depth > world.Cells.Length)
                    throw new InvalidOperationException("Generated drainage traversal exceeded world size.");
            }

            while (depth > 0)
                state[stack[--depth]] = 2;
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
            largestShare is >= 0.20 and <= 0.86 &&
            secondShare >= 0.04 &&
            quality.TinyIslandCount <= Math.Max(24, summary.Cells / 450) &&
            quality.BoundaryOceanRatio >= 0.90f &&
            quality.MountainRangeCount > 0 &&
            quality.LongestRiver >= 5 &&
            quality.LongRiverStraightFraction <= 0.50f &&
            quality.LongCoastAxisFraction <= 0.60f;

        // Two-continent worlds remain valid, but 3–4 major landmasses score
        // better so candidate selection no longer converges on the same macro
        // composition for almost every seed.
        var continentScore = quality.ContinentCount switch
        {
            3 => 2.35,
            4 => 2.20,
            2 => 1.55,
            5 => 1.15,
            1 => 0.35,
            _ => 0.0
        };

        var dominantPenalty = largestShare switch
        {
            > 0.86 => (largestShare - 0.86) * 12.0 + 1.4,
            > 0.75 => (largestShare - 0.75) * 5.0,
            < 0.30 => (0.30 - largestShare) * 2.0,
            _ => 0.0
        };

        return
            continentScore +
            Math.Min(1.4, secondShare * 5.0) +
            Math.Min(1.1, quality.CoastlineComplexity * 0.075) +
            Math.Min(1.0, quality.MountainRangeCount * 0.12) +
            Math.Min(1.0, quality.LongestRiver * 0.022) +
            Math.Min(0.8, quality.TributaryCount * 0.035) -
            Math.Abs(summary.LandRatio - targetLand) * 4.0 -
            dominantPenalty -
            quality.LongRiverStraightFraction * 2.8 -
            quality.LongCoastAxisFraction * 2.4 -
            Math.Max(0, quality.LongestRiverStraightRun - 6) * 0.05 -
            Math.Max(0, quality.LongestCoastAxisRun - 9) * 0.035 -
            Math.Max(0f, quality.RockyLandRatio - 0.22f) * 3.0 -
            Math.Min(1.5, quality.TinyIslandCount / 20.0) -
            Math.Max(0.0, 0.94 - quality.BoundaryOceanRatio) * 6.0;
    }

}
