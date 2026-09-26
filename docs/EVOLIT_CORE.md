# Evolit Core, 0.0.6 foundation through 0.0.9 world generation rework

## Responsibility boundary

`Evolit.Core` is a regular `net8.0` library and has no Godot dependency. It owns deterministic simulation state: fixed-step time, RNG streams, logical world topology, environment arrays, compact organism state, shared immutable genomes, lineages and snapshots.

The Godot application owns rendering, input, UI, camera, current hex presentation and compatibility with the 0.0.5 demo layer. Dependency direction is one-way:

`Evolit (Godot)` → `Evolit.Core`

`Evolit.Core` never references Godot.

## Migration boundary in 0.0.6

The current `DemoWorldDataProvider` remains the legacy presentation/demo source for the existing HUD, selection, statistics, events and map visuals. From 0.0.8, procedural generation creates canonical Core topology/environment first; `WorldMapGenerator` is only a Godot presentation adapter over that result. `CoreSimulationHost` consumes the same generated Core state directly and creates a small founder set matching the visible demo entities. No parallel simulation environment is created.

Core state is canonical only for new Core features. The old demo population evolution is intentionally not migrated in 0.0.6. Later versions can replace individual legacy systems incrementally.

## Time and scheduler

Core advances only through fixed `0.1 s` steps. The Godot adapter converts frame time and selected speed into a number of fixed steps; FPS never enters biological formulas. `SimulationScheduler` runs systems at integer tick intervals. The scheduler currently runs organism updates every tick and separate climate, atmosphere, humidity, hydrology, resources and light systems at deterministic multi-rate intervals.

`SimulationMode.Live` and `SimulationMode.Bootstrap` share the same Core. Bootstrap-specific fidelity/rates can be introduced later without a second engine.

## Deterministic RNG

Core uses xoshiro256** with SplitMix64 seeding. Named deterministic streams are separated for world, genetics, mutation, environment and organisms. RNG state is persisted in snapshots.

## World topology

Simulation uses stable `CellId` values derived from logical axial coordinates. `WorldTopology` stores adjacency in CSR-style arrays and exposes neighbor spans. High-level systems ask the topology for neighbors and do not assume six entries, leaving room for a different topology later without implementing a sphere in 0.0.6.

## Environment

`EnvironmentStore` is a dense array store keyed through topology. Foundation fields include elevation, water depth, temperature, humidity, pressure, light availability, mineral/nutrient potential, organic matter, substrate development, geothermal potential and substrate kind.

Biome is not source-of-truth. A future biome/ecosystem label must be derived from environment + surface + life.

Substrate is separate from developed biological soil. 0.0.6 only stores the foundation; formation cycles belong to later versions.

## Organisms and genetics

`OrganismStore` is structure-of-arrays style hot storage. Stable `OrganismId` values survive dense swap-removal. Organisms currently contain cell, genome, lineage, age, energy, health and alive flags only.

Genomes are immutable value records stored once in `GenomeStore`; organisms reference `GenomeId`. `GenomeInheritance` provides deterministic single/two-parent inheritance plus bounded mutation. `PhenotypeCompiler` deliberately separates inherited genome values from expressed phenotype values.

`LineageStore` records founder/parent lineage relationships and creation tick. Speciation is not implemented in 0.0.6.

## Persistence and determinism

`CoreSimulationSnapshot` includes fixed clock, RNG streams, topology, environment, genomes, lineages and organism store. The existing save document carries this snapshot optionally so 0.0.5 saves without Core data remain loadable.

The headless runner contains deterministic replay, save/restore continuation, genetics, organism-store, environment and procedural-world checks plus 1k/5k/10k/25k/50k organism benchmarks and dedicated world-generation/environment benchmarks.

## 0.0.7 physical-world boundary

0.0.7 builds the deterministic physical environment on the 0.0.6 foundation: geology/substrate separation, hydrology, atmosphere/climate, light, resources, bootstrap stabilization and derived environmental-region classification. Biology is deliberately outside this version. See `docs/PHYSICAL_WORLD.md`.

The intended long-term flow is:

`geology → climate → resources/chemistry → microbial/plankton biomass → multicellular producers → consumers → ecosystem → evolution`

Organisms follow the same emergent principle:

`genome → phenotype → morphology → behaviour → ecology → selection`

No modern-Earth species presets are part of the Core architecture.


## 0.0.8 procedural-world boundary

0.0.8 makes procedural generation a Godot-independent Core concern. Seed + generation settings produce topology, coherent macro geography, elevation/bathymetry, geology, terrain-scale hydrology, initial climate/resources and substrate. That canonical Core environment is then stabilized through the same Bootstrap simulation systems used by live play.

The Godot map is a renderer-compatible projection of generated Core state, not a second physical model. New saves persist the presentation map and the canonical Core snapshot so loading does not rerun procedural generation. Biological biomes, plants, animals and microbial ecosystems remain outside 0.0.8.


## 0.0.9 world-generation boundary

0.0.9 changes generation-time geography while preserving the same Core/runtime ownership boundary. Procedural generation now uses pseudo-geological provinces, coast-distance bathymetry, structured uplift and deterministic priority-flood drainage before constructing the canonical EnvironmentStore.

The generator also exposes generation-only province, uplift, coast-distance, basin and river diagnostics to the Godot presentation adapter. These diagnostics do not become a second simulation state.

Runtime climate, hydrology and organism systems remain in Evolit.Core and continue from the generated initial state. 0.0.9 does not introduce biological ecosystems, runtime tectonics or a spherical world.
