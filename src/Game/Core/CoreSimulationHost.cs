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
    SimulationMode Mode,
    int BootstrapTicks,
    bool BootstrapConverged,
    double BootstrapFinalChange);

public sealed class CoreSimulationHost
{
    private double _accumulator;
    private double _lastTickMilliseconds;
    private double _averageTickMilliseconds;
    private double _allocatedBytesPerTick;
    private readonly int _bootstrapTicks;
    private readonly bool _bootstrapConverged;
    private readonly double _bootstrapFinalChange;

    private CoreSimulationHost(
        CoreSimulation simulation,
        int bootstrapTicks = 0,
        bool bootstrapConverged = true,
        double bootstrapFinalChange = 0)
    {
        Simulation = simulation;
        _bootstrapTicks = bootstrapTicks;
        _bootstrapConverged = bootstrapConverged;
        _bootstrapFinalChange = bootstrapFinalChange;
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

        var topology = world.Map.InitialTopology ?? BuildTopology(world.Map);
        var environment = world.Map.InitialEnvironment ?? BuildEnvironment(world.Map, topology);
        var simulation = new CoreSimulation(
            topology,
            environment,
            SeedMixer.FromString(seed),
            SimulationMode.Bootstrap);

        var bootstrap = StabilizeEnvironment(simulation);
        simulation.Mode = SimulationMode.Live;
        SeedFoundationOrganisms(simulation, world);
        return new CoreSimulationHost(simulation, bootstrap.Ticks, bootstrap.Converged, bootstrap.FinalChange);
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
            Simulation.Mode,
            _bootstrapTicks,
            _bootstrapConverged,
            _bootstrapFinalChange);
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
            var depthMeters = cell.WaterDepthMeters;

            environment.SetInitial(id, new EnvironmentCellState(
                ElevationMeters: cell.ElevationMeters,
                WaterDepthMeters: cell.IsWater ? depthMeters : 0f,
                TemperatureCelsius: cell.TemperatureCelsius,
                Humidity: cell.Humidity,
                PressureKPa: cell.PressureKPa,
                LightAvailability: cell.IsWater && depthMeters > 100f ? 0.55f : 1f,
                MineralPotential: cell.MineralPotential,
                NutrientPotential: cell.NutrientPotential,
                OrganicMatter: 0f,
                SubstrateDevelopment: 0f,
                GeothermalPotential: cell.GeothermalPotential,
                Substrate: cell.Substrate));
        }
        return environment;
    }

    private static BootstrapDiagnostics StabilizeEnvironment(CoreSimulation simulation)
    {
        var previous = simulation.Environment.CaptureSnapshot();
        var finalChange = double.PositiveInfinity;
        var ticks = 0;
        var converged = false;

        for (; ticks < BootstrapPolicy.MaximumTicks; ticks += BootstrapPolicy.CheckIntervalTicks)
        {
            simulation.Step(BootstrapPolicy.CheckIntervalTicks);
            finalChange = simulation.Environment.MeasureChange(previous);
            ValidateEnvironment(simulation);
            previous = simulation.Environment.CaptureSnapshot();
            if (ticks + BootstrapPolicy.CheckIntervalTicks >= BootstrapPolicy.MinimumTicks &&
                finalChange <= BootstrapPolicy.ConvergenceThreshold)
            {
                ticks += BootstrapPolicy.CheckIntervalTicks;
                converged = true;
                break;
            }
        }

        return new BootstrapDiagnostics(Math.Min(ticks, BootstrapPolicy.MaximumTicks), converged, finalChange);
    }

    private static void ValidateEnvironment(CoreSimulation simulation)
    {
        foreach (var id in simulation.Topology.Cells)
        {
            var state = simulation.Environment.Get(id);
            var physical = simulation.Environment.GetPhysical(id);
            if (!float.IsFinite(state.ElevationMeters) ||
                !float.IsFinite(state.WaterDepthMeters) || state.WaterDepthMeters < 0f ||
                !float.IsFinite(state.TemperatureCelsius) ||
                !float.IsFinite(state.Humidity) || state.Humidity is < 0f or > 1f ||
                !float.IsFinite(state.PressureKPa) || state.PressureKPa <= 0f ||
                !float.IsFinite(physical.WindX) || !float.IsFinite(physical.WindY))
                throw new InvalidOperationException($"Bootstrap produced an invalid environment cell {id}.");
        }
    }

    private readonly record struct BootstrapDiagnostics(int Ticks, bool Converged, double FinalChange);

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


}
