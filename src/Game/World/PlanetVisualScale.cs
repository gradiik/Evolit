using System;

namespace Evolit.Game;

public static class PlanetVisualScale
{
    public const float PlanetRadius = 3.0f;
    public const float PhysicalReferenceRadiusMeters = 6_371_000f;

    // Physical elevation remains in metres. This exaggeration exists only so
    // kilometre-scale relief remains legible on a radius-3 presentation sphere.
    public const float PlanetReliefExaggeration = 30f;

    public static float OverviewDistance(float surfaceRadius, float verticalFovDegrees, float aspectRatio)
    {
        var verticalHalfAngle = MathF.PI / 180f * verticalFovDegrees * 0.5f;
        var safeAspect = Math.Max(0.1f, aspectRatio);
        var horizontalHalfAngle = MathF.Atan(MathF.Tan(verticalHalfAngle) * safeAspect);
        var limitingHalfAngle = Math.Min(verticalHalfAngle, horizontalHalfAngle);

        // Leave enough room for relief, the selection outline and a readable
        // silhouette instead of fitting the sphere exactly against the window.
        return Math.Max(surfaceRadius + 0.5f, surfaceRadius / MathF.Sin(limitingHalfAngle) * 1.10f);
    }

    public static float RadiusFromElevationMeters(float elevationMeters)
    {
        var clamped = Math.Clamp(elevationMeters, -11_000f, 9_000f);
        var visualOffset =
            clamped / PhysicalReferenceRadiusMeters *
            PlanetRadius *
            PlanetReliefExaggeration;
        return PlanetRadius + visualOffset;
    }
}
