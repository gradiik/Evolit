# Evolit 0.0.8 Procedural World Generation

0.0.8 moves world-generation logic out of the Godot presentation layer and into the Godot-independent Evolit.Core assembly.

The generation contract is:

seed + settings -> topology -> macro geography -> elevation/bathymetry -> geology -> hydrology -> initial climate/resources -> EnvironmentStore -> Core bootstrap -> live simulation.

The generator creates initial conditions only. Once CoreSimulationHost is created, Evolit.Core owns the environment and continues evolving it.

## Determinism

Generation uses SeedMixer-derived streams for macro elevation, regional detail, ridges/basins, geology, climate and humidity. It does not use wall-clock time, GUIDs, frame timing or Godot RNG. The headless runner provides worldgen-verify, worldgen-seeds, worldgen-summary and worldgen-benchmark.

## Geography

Sea level is selected from the generated elevation distribution to keep the configured land amount within a controlled range. Multi-scale fields create coherent continental and regional structure. Edge falloff keeps ocean access around the finite hex region. Coast refinement removes the most obvious isolated land/water artifacts without replacing the underlying seed structure.

Elevation is continuous. Water depth is canonical numeric state; shallow/deep-water terrain labels are presentation classifications. Geological fields correlate mineral and geothermal potential with provinces, uplift and volcanic structure.

## Hydrology

Drainage follows the lowest neighboring elevation. Flow accumulation is propagated from high to low elevation, allowing tributary-like accumulation. High-accumulation closed depressions form deterministic inland lake basins. River presentation is derived from land cells with sufficient accumulated flow and a downhill target.

This is a terrain-scale hydrology foundation, not CFD or an ocean-circulation model.

## Climate and resources

Initial temperature depends on latitude proxy, elevation, climate setting and deterministic regional variation. Humidity diffuses inland from water and gains a small river contribution. Core bootstrap then runs the same physical environment systems used by live simulation until a convergence threshold is met or a bounded maximum tick count is reached.

Minerals correlate with geology. Nutrients derive from minerals, moisture, drainage/sediment and substrate. Initial organic matter remains zero because 0.0.8 does not invent prior biology.

## Godot adapter

WorldMapGenerator is now an adapter over ProceduralWorldGenerator. It maps generated Core cells to existing renderer-compatible WorldHexCell values while retaining canonical elevation/water depth and generation diagnostics. The T inspector exposes flow accumulation, slope, geothermal potential and mineral seed data in addition to live Core environment values.

Existing visual terrain enums remain derived presentation data and are not the source of simulation physics.

## Scope

0.0.8 does not add plants, animals, microbial ecosystems, biological biomes, plate tectonics, CFD, GPU compute or a spherical planet. Biological ecosystems remain future work.

## Generation settings

New Game keeps seed and world size and adds three compact presets: land amount, climate and geological activity. They are stored in save metadata so the procedural input remains reproducible.

## Save/load

New saves persist the generated presentation map together with the canonical Core snapshot. Loading a 0.0.8 save restores those states directly and does not run procedural generation again. Older saves without a persisted map remain compatible: when a Core snapshot exists, the renderer map is reconstructed from it; only legacy saves without Core state fall back to deterministic seed regeneration.

## Validation and diagnostics

ProceduralWorldGenerator validates finite/bounded physical values, land/water presence, controlled land ratio, drainage direction, substrate validity and closed-basin lake invariants before returning a world. Core bootstrap performs another physical-bound validation while stabilizing.

Bootstrap is bounded by minimum/maximum ticks, a check interval and a convergence threshold. Runtime diagnostics report bootstrap ticks, convergence and final change.

The headless world-generation commands report land/water percentages, components, largest-continent share, islands, mountains, rivers, lakes, elevation/water/climate ranges, generation time, Core construction time, bootstrap time/ticks and convergence. The benchmark reports individual generation-stage timings rather than only one total.


## In-game generation diagnostics

Hold T over a cell to inspect live Core environment plus generation flow/slope/geothermal/mineral seed data. F4 cycles developer-only terrain layers for elevation, water depth, flow accumulation, temperature, humidity, substrate, minerals and the initial derived environmental region. Detail/fine terrain decoration is hidden while a diagnostic layer is active so the field remains readable.
