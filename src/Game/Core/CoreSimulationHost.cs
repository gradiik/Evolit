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
    double P95TickMilliseconds,
    double BacklogSimulationSeconds,
    double DroppedSimulationSeconds,
    long CappedFrames,
    int LastFrameSteps,
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
    private readonly double[] _tickSamples = new double[128];
    private readonly double[] _tickSampleScratch = new double[128];
    private int _tickSampleCount;
    private int _tickSampleCursor;
    private double _droppedSimulationSeconds;
    private long _cappedFrames;
    private int _lastFrameSteps;
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
        _lastFrameSteps = 0;
        if (deltaSeconds <= 0 || speed.Paused)
            return 0;

        _accumulator += deltaSeconds * speed.Multiplier;
        var cappedThisFrame = false;

        // Keep an explicit bounded backlog so a slow frame cannot create an
        // unbounded catch-up spiral. Any cap is surfaced through diagnostics.
        var maximumBacklog = speed.Multiplier >= SimulationSpeedState.MaxMultiplier
            ? 4.0
            : speed.Multiplier >= 16
                ? 2.5
                : 1.5;
        if (_accumulator > maximumBacklog)
        {
            _droppedSimulationSeconds += _accumulator - maximumBacklog;
            _accumulator = maximumBacklog;
            cappedThisFrame = true;
        }

        var fixedStep = Simulation.Clock.FixedDeltaSeconds;
        var requestedSteps = (int)Math.Floor(_accumulator / fixedStep);
        if (requestedSteps <= 0)
            return 0;

        // High speed is best-effort and must leave time for rendering/input.
        // Normal speeds get a slightly smaller budget because they usually need
        // at most one or a few fixed steps per render frame.
        var frameBudgetMs = speed.Multiplier >= SimulationSpeedState.MaxMultiplier
            ? 10.0
            : speed.Multiplier >= 16
                ? 8.0
                : 6.0;

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var frameStarted = Stopwatch.GetTimestamp();
        var steps = 0;
        var totalTickMs = 0.0;

        while (steps < requestedSteps)
        {
            var tickStarted = Stopwatch.GetTimestamp();
            Simulation.Step();
            var tickMs = Stopwatch.GetElapsedTime(tickStarted).TotalMilliseconds;
            RecordTickSample(tickMs);
            totalTickMs += tickMs;
            steps++;

            if (steps < requestedSteps &&
                Stopwatch.GetElapsedTime(frameStarted).TotalMilliseconds >= frameBudgetMs)
            {
                cappedThisFrame = true;
                break;
            }
        }

        _accumulator -= steps * fixedStep;
        if (cappedThisFrame)
            _cappedFrames++;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        _lastFrameSteps = steps;
        _lastTickMilliseconds = totalTickMs / steps;
        _averageTickMilliseconds = _averageTickMilliseconds <= 0
            ? _lastTickMilliseconds
            : _averageTickMilliseconds * 0.92 + _lastTickMilliseconds * 0.08;
        _allocatedBytesPerTick = allocated / (double)steps;
        return steps;
    }

    private void RecordTickSample(double milliseconds)
    {
        _tickSamples[_tickSampleCursor] = milliseconds;
        _tickSampleCursor = (_tickSampleCursor + 1) % _tickSamples.Length;
        if (_tickSampleCount < _tickSamples.Length)
            _tickSampleCount++;
    }

    private double GetP95TickMilliseconds()
    {
        if (_tickSampleCount == 0)
            return 0;

        Array.Copy(_tickSamples, _tickSampleScratch, _tickSampleCount);
        Array.Sort(_tickSampleScratch, 0, _tickSampleCount);
        var index = Math.Clamp(
            (int)Math.Ceiling(_tickSampleCount * 0.95) - 1,
            0,
            _tickSampleCount - 1);
        return _tickSampleScratch[index];
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
            GetP95TickMilliseconds(),
            _accumulator,
            _droppedSimulationSeconds,
            _cappedFrames,
            _lastFrameSteps,
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
        var previous = simulation.Environment.CaptureConvergenceState();
        var finalChange = double.PositiveInfinity;
        var ticks = 0;
        var converged = false;

        for (; ticks < BootstrapPolicy.MaximumTicks; ticks += BootstrapPolicy.CheckIntervalTicks)
        {
            simulation.Step(BootstrapPolicy.CheckIntervalTicks);
            finalChange = simulation.Environment.MeasureChangeAndRefresh(previous);
            simulation.Environment.ValidatePhysicalBounds();
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
