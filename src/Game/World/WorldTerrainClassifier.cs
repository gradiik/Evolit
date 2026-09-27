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
        float tectonicUplift = 0f,
        int coastDistance = -1)
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
        var lowCoastalLand = coastDistance is >= 0 and <= 2 && elevation < 1800f;

        var mountain =
            (elevation >= 1700f && slope >= 250f && relief >= 1100f) ||
            (elevation >= 3000f && (slope >= 180f || relief >= 900f)) ||
            (elevation >= 5000f && relief >= 700f);

        if (mountain)
            return HexTerrainType.Mountain;

        var rocky = !lowCoastalLand && (
            // A steep local slope on low land should still read as a coast or
            // plain, not a wide rocky band around the whole island.
            (elevation >= 750f && slope >= 190f) ||
            (elevation >= 900f && relief >= 900f) ||
            (substrate == SubstrateKind.BareRock && elevation >= 950f && (slope >= 120f || relief >= 600f)) ||
            (substrate == SubstrateKind.Basalt && tectonicUplift >= 0.55f && (slope >= 120f || relief >= 600f)));

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
