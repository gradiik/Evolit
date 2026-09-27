using System;
using System.Collections.Generic;
using Evolit.Core;

internal static class PlanetVerification
{
    public static void Run()
    {
        AssertTopology();
        AssertDeterministicGeneration();
        AssertSnapshotRoundTrip();
        Console.WriteLine("PLANET VERIFY PASS");
    }

    private static void AssertTopology()
    {
        const int frequency = 8;
        var (topology, geometry) = PlanetTopologyFactory.Build(frequency);
        var expected = PlanetGenerationScale.CellCountForFrequency(frequency);
        if (topology.Kind != WorldTopologyKind.Planet || topology.Count != expected || geometry.Count != expected)
            throw new InvalidOperationException("Planet topology count/kind mismatch.");

        var ids = new HashSet<CellId>();
        var pentagons = 0;
        var visited = new bool[topology.Count];
        var queue = new Queue<int>();

        for (var i = 0; i < topology.Count; i++)
        {
            var id = topology.GetCellId(i);
            if (!id.IsPlanet || id.PlanetIndex != i || !ids.Add(id))
                throw new InvalidOperationException($"Invalid or duplicate planet cell id at {i}.");

            if (!topology.TryGetSurfaceDirection(i, out var direction) ||
                Math.Abs(direction.Length - 1f) > 0.0005f)
                throw new InvalidOperationException($"Planet cell {i} has a non-normalized surface direction.");

            var neighbors = topology.GetNeighborIndices(i);
            if (neighbors.Length == 5)
                pentagons++;
            else if (neighbors.Length != 6)
                throw new InvalidOperationException($"Planet cell {i} has degree {neighbors.Length}.");

            var unique = new HashSet<int>();
            for (var n = 0; n < neighbors.Length; n++)
            {
                var neighbor = neighbors[n];
                if (neighbor == i || !unique.Add(neighbor))
                    throw new InvalidOperationException($"Planet cell {i} has duplicate/self neighbor.");

                var reverse = topology.GetNeighborIndices(neighbor);
                var found = false;
                for (var r = 0; r < reverse.Length; r++)
                {
                    if (reverse[r] == i)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new InvalidOperationException($"Planet adjacency {i}<->{neighbor} is not mutual.");
            }

            var polygon = geometry.GetPolygon(i);
            if (polygon.Length != neighbors.Length)
                throw new InvalidOperationException($"Planet cell {i} polygon/neighbor count mismatch.");

            var areaNormal = CoreVector3.Zero;
            for (var p = 0; p < polygon.Length; p++)
            {
                var corner = polygon[p];
                if (Math.Abs(corner.Length - 1f) > 0.0005f)
                    throw new InvalidOperationException($"Planet cell {i} has a non-normalized polygon corner.");
                areaNormal += CoreVector3.Cross(corner, polygon[(p + 1) % polygon.Length]);
            }
            if (CoreVector3.Dot(areaNormal, direction) <= 0f)
                throw new InvalidOperationException($"Planet cell {i} polygon winding is inverted.");
        }

        if (pentagons != 12)
            throw new InvalidOperationException($"Planet topology has {pentagons} pentagons instead of 12.");

        visited[0] = true;
        queue.Enqueue(0);
        var connected = 0;
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            connected++;
            var neighbors = topology.GetNeighborIndices(at);
            for (var n = 0; n < neighbors.Length; n++)
            {
                var next = neighbors[n];
                if (visited[next])
                    continue;
                visited[next] = true;
                queue.Enqueue(next);
            }
        }

        if (connected != topology.Count)
            throw new InvalidOperationException($"Planet topology graph is disconnected: {connected}/{topology.Count}.");
    }

    private static void AssertDeterministicGeneration()
    {
        var settings = new PlanetGenerationSettings(
            "planet-determinism",
            8,
            WorldLandAmount.Normal,
            WorldClimate.Temperate,
            GeologicalActivity.Normal);
        var a = PlanetWorldGenerator.Generate(settings);
        var b = PlanetWorldGenerator.Generate(settings);

        if (a.Cells.Length != b.Cells.Length)
            throw new InvalidOperationException("Same-seed planet cell counts differ.");

        for (var i = 0; i < a.Cells.Length; i++)
        {
            if (a.Cells[i] != b.Cells[i])
                throw new InvalidOperationException($"Same-seed planet generation differs at cell {i}.");
            if (a.Geometry.Centers[i] != b.Geometry.Centers[i])
                throw new InvalidOperationException($"Same-seed planet geometry differs at cell {i}.");
        }

        var different = PlanetWorldGenerator.Generate(settings with { Seed = "planet-determinism-other" });
        var changed = false;
        for (var i = 0; i < a.Cells.Length; i++)
        {
            if (Math.Abs(a.Cells[i].ElevationMeters - different.Cells[i].ElevationMeters) > 0.001f)
            {
                changed = true;
                break;
            }
        }
        if (!changed)
            throw new InvalidOperationException("Different planet seeds produced identical elevation.");
    }

    private static void AssertSnapshotRoundTrip()
    {
        var generated = PlanetWorldGenerator.Generate(new PlanetGenerationSettings("planet-snapshot", 6));
        var simulation = new CoreSimulation(
            generated.Topology,
            generated.Environment,
            SeedMixer.FromString("planet-snapshot"),
            SimulationMode.Live);
        simulation.Step(20);

        var json = CoreSnapshotSerializer.Serialize(simulation.CaptureSnapshot());
        var snapshot = CoreSnapshotSerializer.Deserialize(json);
        var restored = CoreSimulation.Restore(snapshot);

        if (restored.Topology.Kind != WorldTopologyKind.Planet ||
            restored.Topology.Count != generated.Topology.Count ||
            restored.Topology.SurfaceDirections.Length != generated.Topology.Count)
            throw new InvalidOperationException("Planet topology did not survive Core snapshot round-trip.");

        var first = restored.Topology.GetCellId(0);
        if (!first.IsPlanet || !restored.Topology.TryGetSurfaceDirection(0, out var direction) ||
            Math.Abs(direction.Length - 1f) > 0.0005f)
            throw new InvalidOperationException("Planet snapshot lost stable cell geometry.");
    }
}
