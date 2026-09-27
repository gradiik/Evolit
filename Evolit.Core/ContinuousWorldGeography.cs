using System;
using System.Collections.Generic;

namespace Evolit.Core;

internal readonly record struct ContinuousGeographySample(
    float ElevationMeters,
    float Continentalness,
    float Convergence,
    float Divergence,
    float TectonicUplift,
    float BoundaryStrength,
    int GeologicalRegionId,
    int MacroplateId);

internal sealed class ContinuousWorldGeography
{
    private const float MinCoordinate = -1.18f;
    private const float MaxCoordinate = 1.18f;
    private const float CoordinateSpan = MaxCoordinate - MinCoordinate;
    private const float HexYScale = 0.8660254f;

    private readonly record struct Macroplate(
        float X,
        float Y,
        float MotionX,
        float MotionY,
        bool Continental,
        float Crust,
        float Volcanism);

    private readonly record struct GeologicalRegion(
        float X,
        float Y,
        int MacroplateId,
        float CrustOffset);

    private readonly int _resolution;
    private readonly ulong _seed;
    private readonly Macroplate[] _macroplates;
    private readonly GeologicalRegion[] _regions;
    private readonly float[] _elevation;
    private readonly float[] _continentalness;
    private readonly float[] _convergence;
    private readonly float[] _divergence;
    private readonly float[] _uplift;
    private readonly float[] _boundaryStrength;
    private readonly int[] _regionId;
    private readonly int[] _macroplateId;

    private ContinuousWorldGeography(
        int resolution,
        ulong seed,
        WorldGeographyStyle style,
        Macroplate[] macroplates,
        GeologicalRegion[] regions,
        float[] elevation,
        float[] continentalness,
        float[] convergence,
        float[] divergence,
        float[] uplift,
        float[] boundaryStrength,
        int[] regionId,
        int[] macroplateId)
    {
        _resolution = resolution;
        _seed = seed;
        Style = style;
        _macroplates = macroplates;
        _regions = regions;
        _elevation = elevation;
        _continentalness = continentalness;
        _convergence = convergence;
        _divergence = divergence;
        _uplift = uplift;
        _boundaryStrength = boundaryStrength;
        _regionId = regionId;
        _macroplateId = macroplateId;
    }

    public WorldGeographyStyle Style { get; }
    public int Resolution => _resolution;
    public int GeologicalRegionCount => _regions.Length;
    public int MacroplateCount => _macroplates.Length;

    public bool IsMacroplateContinental(int macroplateId) =>
        macroplateId >= 0 &&
        macroplateId < _macroplates.Length &&
        _macroplates[macroplateId].Continental;

    public float MacroplateVolcanism(int macroplateId) =>
        macroplateId >= 0 && macroplateId < _macroplates.Length
            ? _macroplates[macroplateId].Volcanism
            : 0f;

    public float MacroplateCrust(int macroplateId) =>
        macroplateId >= 0 && macroplateId < _macroplates.Length
            ? _macroplates[macroplateId].Crust
            : 0f;

    public static ContinuousWorldGeography Build(
        WorldGenerationSettings settings,
        ulong seed)
    {
        var style = BuildStyle(seed);
        var macroplates = BuildMacroplates(style, seed);
        var regions = BuildRegions(settings, style, macroplates, seed);
        var resolution = settings.ContinuousResolution > 0
            ? Math.Clamp(settings.ContinuousResolution, 128, 384)
            : Math.Clamp(settings.Radius * 3, 160, 304);
        var length = resolution * resolution;

        var elevation = new float[length];
        var continentalness = new float[length];
        var convergence = new float[length];
        var divergence = new float[length];
        var uplift = new float[length];
        var boundaryStrength = new float[length];
        var regionId = new int[length];
        var macroplateId = new int[length];

        for (var yIndex = 0; yIndex < resolution; yIndex++)
        {
            var y = GridCoordinate(yIndex, resolution);
            for (var xIndex = 0; xIndex < resolution; xIndex++)
            {
                var x = GridCoordinate(xIndex, resolution);
                var index = yIndex * resolution + xIndex;
                var sample = Evaluate(
                    x,
                    y,
                    settings,
                    style,
                    macroplates,
                    regions,
                    seed);

                elevation[index] = sample.ElevationMeters;
                continentalness[index] = sample.Continentalness;
                convergence[index] = sample.Convergence;
                divergence[index] = sample.Divergence;
                uplift[index] = sample.TectonicUplift;
                boundaryStrength[index] = sample.BoundaryStrength;
                regionId[index] = sample.GeologicalRegionId;
                macroplateId[index] = sample.MacroplateId;
            }
        }

        return new ContinuousWorldGeography(
            resolution,
            seed,
            style,
            macroplates,
            regions,
            elevation,
            continentalness,
            convergence,
            divergence,
            uplift,
            boundaryStrength,
            regionId,
            macroplateId);
    }

    public ContinuousGeographySample Sample(float x, float y)
    {
        var gx = Math.Clamp(
            (x - MinCoordinate) / CoordinateSpan * (_resolution - 1),
            0f,
            _resolution - 1f);
        var gy = Math.Clamp(
            (y - MinCoordinate) / CoordinateSpan * (_resolution - 1),
            0f,
            _resolution - 1f);

        var x0 = (int)MathF.Floor(gx);
        var y0 = (int)MathF.Floor(gy);
        var x1 = Math.Min(_resolution - 1, x0 + 1);
        var y1 = Math.Min(_resolution - 1, y0 + 1);
        var tx = gx - x0;
        var ty = gy - y0;

        var i00 = y0 * _resolution + x0;
        var i10 = y0 * _resolution + x1;
        var i01 = y1 * _resolution + x0;
        var i11 = y1 * _resolution + x1;

        var nearestX = tx < 0.5f ? x0 : x1;
        var nearestY = ty < 0.5f ? y0 : y1;
        var nearest = nearestY * _resolution + nearestX;

        return new ContinuousGeographySample(
            Bilinear(_elevation[i00], _elevation[i10], _elevation[i01], _elevation[i11], tx, ty),
            Bilinear(_continentalness[i00], _continentalness[i10], _continentalness[i01], _continentalness[i11], tx, ty),
            Bilinear(_convergence[i00], _convergence[i10], _convergence[i01], _convergence[i11], tx, ty),
            Bilinear(_divergence[i00], _divergence[i10], _divergence[i01], _divergence[i11], tx, ty),
            Bilinear(_uplift[i00], _uplift[i10], _uplift[i01], _uplift[i11], tx, ty),
            Bilinear(_boundaryStrength[i00], _boundaryStrength[i10], _boundaryStrength[i01], _boundaryStrength[i11], tx, ty),
            _regionId[nearest],
            _macroplateId[nearest]);
    }

    public WorldGenerationPresentation BuildPresentation(float seaLevel)
    {
        var coast = ExtractContours(
            _elevation,
            seaLevel,
            strength: 1f,
            width: 0.72f + Style.CoastRoughness * 0.34f);

        var ridge = ExtractConvergentBoundarySegments(
            minimumConvergence: 0.11f + (1f - Style.MountainWidth) * 0.08f,
            width: 0.42f + Style.MountainWidth * 0.58f,
            seaLevel: seaLevel);

        return new WorldGenerationPresentation
        {
            Style = Style,
            GeologicalRegionCount = GeologicalRegionCount,
            MacroplateCount = MacroplateCount,
            FieldResolution = Resolution,
            CoastSegments = coast,
            RidgeSegments = ridge
        };
    }

    private WorldGeometrySegment[] ExtractConvergentBoundarySegments(
        float minimumConvergence,
        float width,
        float seaLevel)
    {
        var segments = new List<WorldGeometrySegment>(_resolution * 3);
        var cellStep = CoordinateSpan / (_resolution - 1f);

        for (var y = 0; y < _resolution; y++)
        {
            var cy = GridCoordinate(y, _resolution);
            for (var x = 0; x < _resolution; x++)
            {
                var index = y * _resolution + x;
                var cx = GridCoordinate(x, _resolution);

                if (x + 1 < _resolution)
                {
                    var right = index + 1;
                    if (_macroplateId[index] != _macroplateId[right])
                    {
                        var strength = Math.Max(_convergence[index], _convergence[right]);
                        var localElevation = Math.Max(_elevation[index], _elevation[right]);
                        if (strength >= minimumConvergence &&
                            localElevation >= seaLevel - 120f)
                        {
                            var bx = cx + cellStep * 0.5f;
                            if (ContinuousHexRadius(bx, cy) <= 0.965f)
                            {
                                AddMacroplateBoundarySegment(
                                    bx,
                                    cy,
                                    _macroplateId[index],
                                    _macroplateId[right],
                                    cellStep,
                                    strength,
                                    width,
                                    segments);
                            }
                        }
                    }
                }

                if (y + 1 < _resolution)
                {
                    var down = index + _resolution;
                    if (_macroplateId[index] != _macroplateId[down])
                    {
                        var strength = Math.Max(_convergence[index], _convergence[down]);
                        var localElevation = Math.Max(_elevation[index], _elevation[down]);
                        if (strength >= minimumConvergence &&
                            localElevation >= seaLevel - 120f)
                        {
                            var by = cy + cellStep * 0.5f;
                            if (ContinuousHexRadius(cx, by) <= 0.965f)
                            {
                                AddMacroplateBoundarySegment(
                                    cx,
                                    by,
                                    _macroplateId[index],
                                    _macroplateId[down],
                                    cellStep,
                                    strength,
                                    width,
                                    segments);
                            }
                        }
                    }
                }
            }
        }

        return segments.ToArray();
    }

    private void AddMacroplateBoundarySegment(
        float centerX,
        float centerY,
        int firstPlate,
        int secondPlate,
        float cellStep,
        float strength,
        float width,
        List<WorldGeometrySegment> target)
    {
        if (firstPlate < 0 || secondPlate < 0 ||
            firstPlate >= _macroplates.Length ||
            secondPlate >= _macroplates.Length)
        {
            return;
        }

        var a = _macroplates[firstPlate];
        var b = _macroplates[secondPlate];
        var nx = b.X - a.X;
        var ny = b.Y - a.Y;
        var length = MathF.Sqrt(nx * nx + ny * ny);
        if (length <= 0.0001f)
            return;

        // The plate-center vector approximates the local boundary normal.
        // Its perpendicular therefore gives a ridge tangent independent of the
        // temporary raster axes.
        var tx = -ny / length;
        var ty = nx / length;
        var halfLength = cellStep * 0.78f;

        target.Add(new WorldGeometrySegment(
            centerX - tx * halfLength,
            centerY - ty * halfLength,
            centerX + tx * halfLength,
            centerY + ty * halfLength,
            Math.Clamp(strength, 0f, 1f),
            width));
    }

    private WorldGeometrySegment[] ExtractContours(
        float[] field,
        float threshold,
        float strength,
        float width)
    {
        var segments = new List<WorldGeometrySegment>(_resolution * 4);
        Span<(float X, float Y)> crossings = stackalloc (float X, float Y)[4];

        for (var y = 0; y < _resolution - 1; y++)
        {
            var y0 = GridCoordinate(y, _resolution);
            var y1 = GridCoordinate(y + 1, _resolution);

            for (var x = 0; x < _resolution - 1; x++)
            {
                var x0 = GridCoordinate(x, _resolution);
                var x1 = GridCoordinate(x + 1, _resolution);
                var i00 = y * _resolution + x;
                var i10 = i00 + 1;
                var i01 = i00 + _resolution;
                var i11 = i01 + 1;

                var v00 = field[i00] - threshold;
                var v10 = field[i10] - threshold;
                var v11 = field[i11] - threshold;
                var v01 = field[i01] - threshold;

                var count = 0;
                AddCrossing(v00, v10, x0, y0, x1, y0, ref count, crossings);
                AddCrossing(v10, v11, x1, y0, x1, y1, ref count, crossings);
                AddCrossing(v11, v01, x1, y1, x0, y1, ref count, crossings);
                AddCrossing(v01, v00, x0, y1, x0, y0, ref count, crossings);

                if (count < 2)
                    continue;

                var centerX = (x0 + x1) * 0.5f;
                var centerY = (y0 + y1) * 0.5f;
                if (ContinuousHexRadius(centerX, centerY) > 0.965f)
                    continue;

                if (count == 2)
                {
                    AddSegment(crossings[0], crossings[1], strength, width, segments);
                    continue;
                }

                var centerValue = (v00 + v10 + v11 + v01) * 0.25f;
                if (centerValue >= 0f)
                {
                    AddSegment(crossings[0], crossings[1], strength, width, segments);
                    AddSegment(crossings[2], crossings[3], strength, width, segments);
                }
                else
                {
                    AddSegment(crossings[0], crossings[3], strength, width, segments);
                    AddSegment(crossings[1], crossings[2], strength, width, segments);
                }
            }
        }

        return segments.ToArray();
    }

    private static void AddCrossing(
        float a,
        float b,
        float ax,
        float ay,
        float bx,
        float by,
        ref int count,
        Span<(float X, float Y)> crossings)
    {
        if ((a >= 0f) == (b >= 0f))
            return;

        var denominator = a - b;
        var t = Math.Abs(denominator) <= 0.000001f
            ? 0.5f
            : Math.Clamp(a / denominator, 0f, 1f);

        crossings[count++] = (
            ax + (bx - ax) * t,
            ay + (by - ay) * t);
    }

    private static void AddSegment(
        (float X, float Y) a,
        (float X, float Y) b,
        float strength,
        float width,
        List<WorldGeometrySegment> target)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        if (dx * dx + dy * dy < 0.0000004f)
            return;

        target.Add(new WorldGeometrySegment(
            a.X,
            a.Y,
            b.X,
            b.Y,
            strength,
            width));
    }

    private static ContinuousGeographySample Evaluate(
        float x0,
        float y0,
        WorldGenerationSettings settings,
        WorldGeographyStyle style,
        Macroplate[] macroplates,
        GeologicalRegion[] regions,
        ulong seed)
    {
        var warpScale = 1.8f + style.CoastScale * 1.4f;
        var warpAmount = 0.10f + style.ContinentalFragmentation * 0.15f;
        var warpX = (Fbm(x0 * warpScale + 3.1f, y0 * warpScale - 5.4f, SeedMixer.Combine(seed, 101), 3) - 0.5f) * warpAmount;
        var warpY = (Fbm(x0 * warpScale - 4.7f, y0 * warpScale + 2.8f, SeedMixer.Combine(seed, 103), 3) - 0.5f) * warpAmount;
        var x = x0 + warpX;
        var y = y0 + warpY;

        FindRegions(regions, x, y, out var firstRegion, out var secondRegion, out var otherMacroRegion, out var d1, out var d2, out var dOther);

        var r1 = regions[firstRegion];
        var r2 = regions[secondRegion];
        var p1 = macroplates[r1.MacroplateId];
        var p2 = macroplates[regions[otherMacroRegion].MacroplateId];

        var localGap = Math.Max(0f, MathF.Sqrt(d2) - MathF.Sqrt(d1));
        var localBlend = 0.5f * MathF.Exp(-localGap * 8f);
        var crust = Lerp(
            p1.Crust + r1.CrustOffset,
            macroplates[r2.MacroplateId].Crust + r2.CrustOffset,
            localBlend);

        var macroGap = Math.Max(0f, MathF.Sqrt(dOther) - MathF.Sqrt(d1));
        var boundaryWidth = 9f - style.MountainWidth * 3.5f;
        var boundaryStrength = MathF.Exp(-macroGap * boundaryWidth);

        var nx = p2.X - p1.X;
        var ny = p2.Y - p1.Y;
        var length = MathF.Sqrt(nx * nx + ny * ny);
        if (length > 0.0001f)
        {
            nx /= length;
            ny /= length;
        }

        var relative =
            (p1.MotionX - p2.MotionX) * nx +
            (p1.MotionY - p2.MotionY) * ny;

        var convergence = Math.Clamp(relative, 0f, 1.5f) * boundaryStrength;
        var divergence = Math.Clamp(-relative, 0f, 1.5f) * boundaryStrength;

        var broad = Fbm(
            x0 * (0.85f + style.CoastScale * 0.55f) + 7f,
            y0 * (0.85f + style.CoastScale * 0.55f) - 11f,
            SeedMixer.Combine(seed, 107),
            4) - 0.5f;
        var regional = Fbm(
            x0 * (2.1f + style.CoastScale * 1.6f) - 13f,
            y0 * (2.1f + style.CoastScale * 1.6f) + 17f,
            SeedMixer.Combine(seed, 109),
            3) - 0.5f;
        var local = Fbm(
            x0 * (5.5f + style.CoastScale * 4.0f) + 19f,
            y0 * (5.5f + style.CoastScale * 4.0f) - 23f,
            SeedMixer.Combine(seed, 113),
            2) - 0.5f;

        var plateAngle = Hash01(r1.MacroplateId + 301, 313, SeedMixer.Combine(seed, 127)) * MathF.PI * 2f;
        var u = x0 * MathF.Cos(plateAngle) + y0 * MathF.Sin(plateAngle);
        var v = -x0 * MathF.Sin(plateAngle) + y0 * MathF.Cos(plateAngle);
        var wave =
            MathF.Sin(
                u * (3.8f + style.PeninsulaStrength * 3.6f) +
                MathF.Sin(v * (2.2f + style.BayStrength * 2.8f)) * 0.9f) *
            0.5f;

        var landBias = settings.LandAmount switch
        {
            WorldLandAmount.Low => -0.10f,
            WorldLandAmount.High => 0.10f,
            _ => 0f
        };

        var continentalBoundary =
            p1.Continental && p2.Continental
                ? boundaryStrength
                : 0f;
        var rift =
            divergence * (0.16f + style.RiftStrength * 0.42f) +
            continentalBoundary * Math.Max(0f, 0.50f - relative) * style.ContinentalFragmentation * 0.16f;

        var arcNoise = Fbm(
            x0 * (7.0f + style.IslandArcDensity * 4.0f) + 41f,
            y0 * (7.0f + style.IslandArcDensity * 4.0f) - 43f,
            SeedMixer.Combine(seed, 149),
            2);
        var oceanicArc =
            !p1.Continental && !p2.Continental
                ? convergence *
                  style.IslandArcDensity *
                  Math.Max(0f, arcNoise - (0.61f - style.IslandArcDensity * 0.08f)) *
                  (0.45f + (p1.Volcanism + p2.Volcanism) * 0.32f)
                : 0f;

        var continentalMargin = p1.Continental != p2.Continental
            ? boundaryStrength
            : 0f;
        var shelfNoise = Fbm(
            x0 * (5.4f + style.CoastScale * 3.2f) - 47f,
            y0 * (5.4f + style.CoastScale * 3.2f) + 53f,
            SeedMixer.Combine(seed, 157),
            3);
        var shelfFragment =
            continentalMargin *
            style.ContinentalFragmentation *
            Math.Max(0f, shelfNoise - (0.64f - style.IslandArcDensity * 0.06f)) *
            (0.34f + style.PeninsulaStrength * 0.42f);

        var coastDetail =
            regional * (0.28f + style.CoastRoughness * 0.34f) +
            local * (0.04f + style.CoastRoughness * 0.10f) +
            wave * (0.03f + style.PeninsulaStrength * 0.08f);

        var edgeOcean = MathF.Pow(
            Math.Clamp((ContinuousHexRadius(x0, y0) - 0.70f) / 0.30f, 0f, 1f),
            1.85f) * 3.8f;

        var value =
            crust * 0.92f +
            broad * 0.50f +
            coastDetail +
            oceanicArc +
            shelfFragment -
            rift -
            edgeOcean +
            landBias;

        var uplift = Math.Clamp(
            MathF.Pow(Math.Clamp(convergence, 0f, 1f), 0.82f) *
            (0.56f + style.MountainSharpness * 0.68f),
            0f,
            1f);

        return new ContinuousGeographySample(
            ElevationMeters: value * 2600f,
            Continentalness: Math.Clamp(value, -1.6f, 1.6f),
            Convergence: convergence,
            Divergence: divergence,
            TectonicUplift: uplift,
            BoundaryStrength: boundaryStrength,
            GeologicalRegionId: firstRegion,
            MacroplateId: r1.MacroplateId);
    }

    private static WorldGeographyStyle BuildStyle(ulong seed)
    {
        return new WorldGeographyStyle(
            ContinentalFragmentation: 0.22f + Hash01(11, 17, seed) * 0.68f,
            CoastRoughness: 0.18f + Hash01(19, 23, seed) * 0.72f,
            CoastScale: 0.25f + Hash01(29, 31, seed) * 0.65f,
            PeninsulaStrength: 0.18f + Hash01(37, 41, seed) * 0.72f,
            BayStrength: 0.18f + Hash01(43, 47, seed) * 0.72f,
            IslandArcDensity: 0.15f + Hash01(53, 59, seed) * 0.72f,
            RiftStrength: 0.18f + Hash01(61, 67, seed) * 0.72f,
            MountainSharpness: 0.25f + Hash01(71, 73, seed) * 0.65f,
            MountainWidth: 0.22f + Hash01(79, 83, seed) * 0.64f,
            PlateauStrength: 0.18f + Hash01(89, 97, seed) * 0.68f,
            PlainSmoothness: 0.30f + Hash01(101, 103, seed) * 0.58f,
            RiverMeander: 0.18f + Hash01(107, 109, seed) * 0.72f,
            ShelfWidth: 0.20f + Hash01(113, 127, seed) * 0.68f,
            OceanBasinDepth: 0.28f + Hash01(131, 137, seed) * 0.62f);
    }

    private static Macroplate[] BuildMacroplates(
        WorldGeographyStyle style,
        ulong seed)
    {
        var count = 5 + (int)MathF.Round(style.ContinentalFragmentation * 4f);
        count = Math.Clamp(count, 5, 9);

        var continentalTarget =
            Math.Clamp(
                3 + (int)MathF.Round(style.ContinentalFragmentation * 2f),
                3,
                Math.Max(3, count - 2));

        var scores = new (float Score, int Index)[count];
        for (var i = 0; i < count; i++)
            scores[i] = (Hash01(i + 151, 157, seed), i);
        Array.Sort(scores, static (a, b) => b.Score.CompareTo(a.Score));

        var continental = new bool[count];
        for (var i = 0; i < continentalTarget; i++)
            continental[scores[i].Index] = true;

        var macroplates = new Macroplate[count];
        const float goldenAngle = 2.39996323f;
        var rotation = Hash01(163, 167, seed) * MathF.PI * 2f;

        for (var i = 0; i < count; i++)
        {
            var radial = MathF.Sqrt((i + 0.55f) / count) * (0.66f + Hash01(i + 173, 179, seed) * 0.20f);
            var angle = rotation + i * goldenAngle + SignedHash(seed, 181 + i) * 0.30f;
            var x = MathF.Cos(angle) * radial + SignedHash(seed, 211 + i) * 0.075f;
            var y = MathF.Sin(angle) * radial + SignedHash(seed, 241 + i) * 0.075f;

            var motionAngle = Hash01(i + 271, 277, SeedMixer.Combine(seed, 41)) * MathF.PI * 2f;
            var speed = 0.34f + Hash01(i + 281, 283, SeedMixer.Combine(seed, 43)) * 0.70f;
            var crust = continental[i]
                ? 0.58f + Hash01(i + 293, 307, seed) * 0.46f
                : -0.62f - Hash01(i + 311, 313, seed) * 0.34f;

            macroplates[i] = new Macroplate(
                x,
                y,
                MathF.Cos(motionAngle) * speed,
                MathF.Sin(motionAngle) * speed,
                continental[i],
                crust,
                Hash01(i + 317, 331, SeedMixer.Combine(seed, 47)));
        }

        return macroplates;
    }

    private static GeologicalRegion[] BuildRegions(
        WorldGenerationSettings settings,
        WorldGeographyStyle style,
        Macroplate[] macroplates,
        ulong seed)
    {
        var count = Math.Clamp(
            (int)MathF.Round(settings.Radius * (0.82f + style.CoastScale * 0.58f)),
            56,
            150);

        var regions = new GeologicalRegion[count];
        for (var i = 0; i < count; i++)
        {
            var attempt = 0;
            float q;
            float r;

            do
            {
                q = SignedHash(seed, 4000 + i * 7 + attempt * 2) * 0.91f;
                r = SignedHash(seed, 4001 + i * 7 + attempt * 2) * 0.91f;
                attempt++;
            }
            while (Math.Max(Math.Abs(q), Math.Max(Math.Abs(r), Math.Abs(-q - r))) > 0.91f && attempt < 12);

            var hexRadius = Math.Max(Math.Abs(q), Math.Max(Math.Abs(r), Math.Abs(-q - r)));
            if (hexRadius > 0.91f)
            {
                var scale = 0.90f / hexRadius;
                q *= scale;
                r *= scale;
            }

            var x = q + r * 0.5f;
            var y = r * HexYScale;
            var warpedX =
                x +
                (Fbm(x * 2.2f + 11f, y * 2.2f - 13f, SeedMixer.Combine(seed, 601), 2) - 0.5f) *
                style.ContinentalFragmentation * 0.18f;
            var warpedY =
                y +
                (Fbm(x * 2.2f - 17f, y * 2.2f + 19f, SeedMixer.Combine(seed, 607), 2) - 0.5f) *
                style.ContinentalFragmentation * 0.18f;

            var macroplateId = FindNearestMacroplate(macroplates, warpedX, warpedY);
            regions[i] = new GeologicalRegion(
                x,
                y,
                macroplateId,
                SignedHash(seed, 5000 + i) * (0.06f + style.CoastRoughness * 0.09f));
        }

        return regions;
    }

    private static void FindRegions(
        GeologicalRegion[] regions,
        float x,
        float y,
        out int first,
        out int second,
        out int otherMacroRegion,
        out float firstDistance,
        out float secondDistance,
        out float otherMacroDistance)
    {
        first = 0;
        second = 0;
        otherMacroRegion = 0;
        firstDistance = float.MaxValue;
        secondDistance = float.MaxValue;
        otherMacroDistance = float.MaxValue;

        for (var i = 0; i < regions.Length; i++)
        {
            var dx = x - regions[i].X;
            var dy = y - regions[i].Y;
            var distance = dx * dx + dy * dy;

            if (distance < firstDistance)
            {
                second = first;
                secondDistance = firstDistance;
                first = i;
                firstDistance = distance;
            }
            else if (distance < secondDistance)
            {
                second = i;
                secondDistance = distance;
            }
        }

        var firstMacro = regions[first].MacroplateId;
        for (var i = 0; i < regions.Length; i++)
        {
            if (regions[i].MacroplateId == firstMacro)
                continue;

            var dx = x - regions[i].X;
            var dy = y - regions[i].Y;
            var distance = dx * dx + dy * dy;
            if (distance >= otherMacroDistance)
                continue;

            otherMacroDistance = distance;
            otherMacroRegion = i;
        }

        if (otherMacroDistance == float.MaxValue)
        {
            otherMacroRegion = second;
            otherMacroDistance = secondDistance;
        }
    }

    private static int FindNearestMacroplate(
        Macroplate[] macroplates,
        float x,
        float y)
    {
        var best = 0;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < macroplates.Length; i++)
        {
            var dx = x - macroplates[i].X;
            var dy = y - macroplates[i].Y;
            var distance = dx * dx + dy * dy;
            if (distance >= bestDistance)
                continue;

            best = i;
            bestDistance = distance;
        }

        return best;
    }

    private static float ContinuousHexRadius(float x, float y)
    {
        var r = y / HexYScale;
        var q = x - r * 0.5f;
        return Math.Max(Math.Abs(q), Math.Max(Math.Abs(r), Math.Abs(-q - r)));
    }

    private static float GridCoordinate(int index, int resolution)
    {
        return MinCoordinate + index / (float)(resolution - 1) * CoordinateSpan;
    }

    private static float Bilinear(
        float v00,
        float v10,
        float v01,
        float v11,
        float tx,
        float ty)
    {
        var a = Lerp(v00, v10, tx);
        var b = Lerp(v01, v11, tx);
        return Lerp(a, b, ty);
    }

    private static float SignedHash(ulong seed, int channel)
    {
        return Hash01(channel * 17 + 3, channel * 31 + 7, seed) * 2f - 1f;
    }

    private static float Fbm(float x, float y, ulong seed, int octaves)
    {
        var value = 0f;
        var amplitude = 0.5f;
        var frequency = 1f;
        var total = 0f;

        for (var octave = 0; octave < octaves; octave++)
        {
            value += ValueNoise(
                x * frequency,
                y * frequency,
                SeedMixer.Combine(seed, (ulong)octave + 1)) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return total <= 0f ? 0f : value / total;
    }

    private static float ValueNoise(float x, float y, ulong seed)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var tx = Smooth(x - x0);
        var ty = Smooth(y - y0);

        var a = Hash01(x0, y0, seed);
        var b = Hash01(x0 + 1, y0, seed);
        var c = Hash01(x0, y0 + 1, seed);
        var d = Hash01(x0 + 1, y0 + 1, seed);

        return Lerp(
            Lerp(a, b, tx),
            Lerp(c, d, tx),
            ty);
    }

    private static float Hash01(int x, int y, ulong seed)
    {
        var h = SeedMixer.Combine(
            seed,
            unchecked(((ulong)(uint)x << 32) | (uint)y));
        return (float)(h >> 40) * (1f / 16777215f);
    }

    private static float Smooth(float value) => value * value * (3f - 2f * value);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
