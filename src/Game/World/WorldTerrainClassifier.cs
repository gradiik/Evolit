using System;
using Evolit.Core;

namespace Evolit.Game;

public static class WorldTerrainClassifier
{
    public static HexTerrainType Classify(
        float elevationMeters,
        float waterDepthMeters,
        float temperatureCelsius,
        float humidity,
        SubstrateKind substrate,
        HexWaterKind water,
        float localSlopeMeters,
        float localReliefMeters,
        float tectonicUplift = 0f)
    {
        if (water == HexWaterKind.Lake)
            return HexTerrainType.Lake;

        if (water == HexWaterKind.Ocean)
            return waterDepthMeters > 2000f
                ? HexTerrainType.DeepWater
                : HexTerrainType.ShallowWater;

        var slope = Math.Max(0f, localSlopeMeters);
        var relief = Math.Max(0f, localReliefMeters);
        var elevation = Math.Max(0f, elevationMeters);

        var mountain =
            (elevation >= 1200f && slope >= 190f && relief >= 850f) ||
            (elevation >= 2500f && (slope >= 130f || relief >= 650f)) ||
            (elevation >= 4500f && relief >= 450f);

        if (mountain)
            return HexTerrainType.Mountain;

        var rocky =
            slope >= 155f ||
            relief >= 720f ||
            (substrate == SubstrateKind.BareRock && elevation >= 950f && (slope >= 120f || relief >= 600f)) ||
            (substrate == SubstrateKind.Basalt && tectonicUplift >= 0.55f && (slope >= 120f || relief >= 600f));

        if (rocky)
            return HexTerrainType.Rocky;

        if (elevation >= 700f && slope < 190f && relief < 850f)
            return HexTerrainType.Highland;

        if (substrate == SubstrateKind.Sand && elevation < 180f)
            return HexTerrainType.Sand;

        if (humidity < 0.28f && temperatureCelsius > 18f)
            return HexTerrainType.Desert;

        return HexTerrainType.Grassland;
    }
}
