using System;
using System.Collections.Generic;
using System.Diagnostics;
using Evolit.Core;

namespace Evolit.Game.Core;

public readonly record struct CoreRuntimeDiagnostics(
    long Tick,
    double SimulationSeconds,
    int OrganismCount,
    int GenomeCount,
    int LineageCount,
    int CellCount,
    double LastTickMilliseconds,
    double AverageTickMilliseconds,
    double AllocatedBytesPerTick,
    SimulationMode Mode);

public sealed class CoreSimulationHost
{
    private double _accumulator;
    private double _lastTickMilliseconds;
    private double _averageTickMilliseconds;
    private double _allocatedBytesPerTick;

    private CoreSimulationHost(CoreSimulation simulation)
    {
        Simulation = simulation;
    }

    public CoreSimulation Simulation { get; }

    public static CoreSimulationHost Create(
        DemoWorldDataProvider world,
        string seed,
        CoreSimulationSnapshot? snapshot = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (snapshot is not null)
            return new CoreSimulationHost(CoreSimulation.Restore(snapshot));

        var topology = BuildTopology(world.Map);
        var environment = BuildEnvironment(world.Map, topology);
        var simulation = new CoreSimulation(
            topology,
            environment,
            SeedMixer.FromString(seed),
            SimulationMode.Live);

        SeedFoundationOrganisms(simulation, world);
        return new CoreSimulationHost(simulation);
    }

    public int AdvanceFrame(double deltaSeconds, SimulationSpeedState speed)
    {
        if (deltaSeconds <= 0 || speed.Paused)
            return 0;

        _accumulator += deltaSeconds * speed.Multiplier;
        var fixedStep = Simulation.Clock.FixedDeltaSeconds;
        var steps = (int)Math.Floor(_accumulator / fixedStep);
        if (steps <= 0)
            return 0;

        _accumulator -= steps * fixedStep;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        Simulation.Step(steps);
        var totalMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        _lastTickMilliseconds = totalMs / steps;
        _averageTickMilliseconds = _averageTickMilliseconds <= 0
            ? _lastTickMilliseconds
            : _averageTickMilliseconds * 0.92 + _lastTickMilliseconds * 0.08;
        _allocatedBytesPerTick = allocated / (double)steps;
        return steps;
    }

    public CoreSimulationSnapshot CaptureSnapshot() => Simulation.CaptureSnapshot();

    public bool TryGetEnvironment(int q, int r, out EnvironmentCellState environment, out PhysicalEnvironmentState physical)
    {
        var id = CellId.FromAxial(q, r);
        if (!Simulation.Topology.TryGetIndex(id, out _))
        {
            environment = default;
            physical = default;
            return false;
        }
        environment = Simulation.Environment.Get(id);
        physical = Simulation.Environment.GetPhysical(id);
        return true;
    }

    public CoreRuntimeDiagnostics GetDiagnostics()
    {
        return new CoreRuntimeDiagnostics(
            Simulation.Clock.TickCount,
            Simulation.Clock.SimulationSeconds,
            Simulation.Organisms.Count,
            Simulation.Genomes.Count,
            Simulation.Lineages.Count,
            Simulation.Topology.Count,
            _lastTickMilliseconds,
            _averageTickMilliseconds,
            _allocatedBytesPerTick,
            Simulation.Mode);
    }

    private static WorldTopology BuildTopology(WorldMap map)
    {
        var builder = new WorldTopologyBuilder();
        foreach (var cell in map.Cells)
        {
            var neighbors = new List<CellId>(6);
            foreach (var coord in cell.Coord.Neighbors())
            {
                if (map.TryGetCell(coord, out _))
                    neighbors.Add(CellId.FromAxial(coord.Q, coord.R));
            }
            builder.Add(CellId.FromAxial(cell.Coord.Q, cell.Coord.R), neighbors);
        }
        return builder.Build();
    }

    private static EnvironmentStore BuildEnvironment(WorldMap map, WorldTopology topology)
    {
        var environment = new EnvironmentStore(topology);
        foreach (var cell in map.Cells)
        {
            var id = CellId.FromAxial(cell.Coord.Q, cell.Coord.R);
            var depthMeters = cell.Terrain switch
            {
                HexTerrainType.River => 2.5f + cell.WaterDepth * 18f,
                HexTerrainType.Lake => 6f + cell.WaterDepth * 120f,
                _ => cell.WaterDepth * 850f
            };

            environment.SetInitial(id, new EnvironmentCellState(
                ElevationMeters: cell.ElevationMeters,
                WaterDepthMeters: cell.IsWater ? depthMeters : 0f,
                TemperatureCelsius: cell.TemperatureCelsius,
                Humidity: cell.Humidity,
                PressureKPa: cell.PressureKPa,
                LightAvailability: cell.IsWater && depthMeters > 100f ? 0.55f : 1f,
                MineralPotential: MineralPotential(cell.Terrain),
                NutrientPotential: cell.IsWater ? 0.35f : 0.22f,
                OrganicMatter: 0f,
                SubstrateDevelopment: 0f,
                GeothermalPotential: cell.Terrain == HexTerrainType.Mountain ? 0.12f : 0.02f,
                Substrate: SubstrateFor(cell.Terrain)));
        }
        return environment;
    }

    private static void SeedFoundationOrganisms(CoreSimulation simulation, DemoWorldDataProvider world)
    {
        var plantGenome = simulation.Genomes.Add(Genome.Create(0.45f, 0.35f, 0.05f, 0.55f, 0.58f, 0.78f, 0.72f));
        var creatureGenome = simulation.Genomes.Add(Genome.Create(0.52f, 0.58f, 0.55f, 0.52f, 0.62f, 0.58f, 0.42f));
        var plantLineage = simulation.Lineages.CreateFounder(plantGenome, simulation.Clock.TickCount);
        var creatureLineage = simulation.Lineages.CreateFounder(creatureGenome, simulation.Clock.TickCount);

        foreach (var entity in world.Entities)
        {
            var cell = world.Map.GetCellAtWorld(entity.WorldPosition);
            if (cell is null)
                continue;

            var isPlant = entity.Kind == DemoEntityKind.Plant;
            simulation.Organisms.Create(
                CellId.FromAxial(cell.Coord.Q, cell.Coord.R),
                isPlant ? plantGenome : creatureGenome,
                isPlant ? plantLineage : creatureLineage,
                Math.Clamp(entity.Energy <= 0 ? 0.8f : entity.Energy, 0f, 1f),
                Math.Clamp(entity.Health, 0f, 1f));
        }
    }

    private static float MineralPotential(HexTerrainType terrain)
    {
        return terrain switch
        {
            HexTerrainType.Mountain => 0.82f,
            HexTerrainType.Rocky => 0.74f,
            HexTerrainType.River => 0.62f,
            HexTerrainType.Sand => 0.38f,
            HexTerrainType.Desert => 0.48f,
            HexTerrainType.Grassland => 0.55f,
            _ => 0.45f
        };
    }

    private static SubstrateKind SubstrateFor(HexTerrainType terrain)
    {
        return terrain switch
        {
            HexTerrainType.Sand => SubstrateKind.Sand,
            HexTerrainType.Mountain => SubstrateKind.BareRock,
            HexTerrainType.Rocky => SubstrateKind.BareRock,
            HexTerrainType.DeepWater => SubstrateKind.Sediment,
            HexTerrainType.ShallowWater => SubstrateKind.Sediment,
            HexTerrainType.Lake => SubstrateKind.Sediment,
            HexTerrainType.River => SubstrateKind.Sediment,
            _ => SubstrateKind.MineralRegolith
        };
    }
}
