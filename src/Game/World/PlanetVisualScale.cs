using System;

namespace Evolit.Game;

public static class PlanetVisualScale
{
    public const float PlanetRadius = 3.0f;
    public const float PhysicalReferenceRadiusMeters = 6_371_000f;

    // Physical elevation remains in metres. This exaggeration exists only so
    // kilometre-scale relief remains legible on a radius-3 presentation sphere.
    public const float PlanetReliefExaggeration = 30f;

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
