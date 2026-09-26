# Evolit Core — 0.0.6 foundation

## Responsibility boundary

`Evolit.Core` is a regular `net8.0` library and has no Godot dependency. It owns deterministic simulation state: fixed-step time, RNG streams, logical world topology, environment arrays, compact organism state, shared immutable genomes, lineages and snapshots.

The Godot application owns rendering, input, UI, camera, current hex presentation and compatibility with the 0.0.5 demo layer. Dependency direction is one-way:

`Evolit (Godot)` → `Evolit.Core`

`Evolit.Core` never references Godot.

## Migration boundary in 0.0.6

The current `DemoWorldDataProvider` remains the legacy presentation/demo source for the existing HUD, selection, statistics, events and map visuals. `CoreSimulationHost` adapts the generated `WorldMap` into Core topology/environment and creates a small founder set matching the visible demo entities. No third Godot entity model is introduced.

Core state is canonical only for new Core features. The old demo population evolution is intentionally not migrated in 0.0.6. Later versions can replace individual legacy systems incrementally.

## Time and scheduler

Core advances only through fixed `0.1 s` steps. The Godot adapter converts frame time and selected speed into a number of fixed steps; FPS never enters biological formulas. `SimulationScheduler` runs systems at integer tick intervals. The foundation currently has an organism system every tick and an environment proof system every 10 ticks.

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

The headless runner contains deterministic replay, save/restore continuation, genetics, organism-store and environment checks plus 1k/5k/10k/25k/50k benchmark scenarios.

## 0.0.7 boundary

0.0.6 is foundation only. 0.0.7 should build the physical planet/environment on this base: geology, substrate dynamics, volcanism, climate, water chemistry, nutrients, organic matter, atmosphere foundation, microbial/plankton biomass and derived ecosystem/biome classification.

The intended long-term flow is:

`geology → climate → resources/chemistry → microbial/plankton biomass → multicellular producers → consumers → ecosystem → evolution`

Organisms follow the same emergent principle:

`genome → phenotype → morphology → behaviour → ecology → selection`

No modern-Earth species presets are part of the Core architecture.
