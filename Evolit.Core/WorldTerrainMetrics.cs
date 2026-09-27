using System;

namespace Evolit.Core;

public static class WorldTerrainMetrics
{
    public static (float[] SlopeMeters, float[] LocalReliefMeters) Compute(
        WorldTopology topology,
        ReadOnlySpan<float> elevationMeters,
        int reliefHops = 2)
    {
        if (topology.Count != elevationMeters.Length)
            throw new ArgumentException("Topology and elevation lengths must match.", nameof(elevationMeters));

        reliefHops = Math.Clamp(reliefHops, 1, 3);
        var slope = new float[elevationMeters.Length];
        var relief = new float[elevationMeters.Length];
        var marks = new int[elevationMeters.Length];
        var queue = new int[Math.Max(1, elevationMeters.Length)];
        var stamp = 0;

        for (var i = 0; i < elevationMeters.Length; i++)
        {
            var origin = elevationMeters[i];
            var land = origin >= 0f;
            var min = origin;
            var max = origin;
            var maxSlope = 0f;

            var direct = topology.GetNeighborIndices(i);
            for (var n = 0; n < direct.Length; n++)
            {
                var value = elevationMeters[direct[n]];
                if ((value >= 0f) != land)
                    continue;
                maxSlope = Math.Max(maxSlope, Math.Abs(origin - value));
            }
            slope[i] = maxSlope;

            stamp++;
            if (stamp == int.MaxValue)
            {
                Array.Clear(marks);
                stamp = 1;
            }

            var head = 0;
            var tail = 0;
            queue[tail++] = i;
            marks[i] = stamp;

            for (var hop = 0; hop < reliefHops; hop++)
            {
                var levelEnd = tail;
                while (head < levelEnd)
                {
                    var at = queue[head++];
                    var neighbors = topology.GetNeighborIndices(at);
                    for (var n = 0; n < neighbors.Length; n++)
                    {
                        var ni = neighbors[n];
                        if (marks[ni] == stamp)
                            continue;
                        marks[ni] = stamp;

                        var value = elevationMeters[ni];
                        if ((value >= 0f) != land)
                            continue;

                        min = Math.Min(min, value);
                        max = Math.Max(max, value);
                        queue[tail++] = ni;
                    }
                }
            }

            relief[i] = Math.Max(0f, max - min);
        }

        return (slope, relief);
    }
}
