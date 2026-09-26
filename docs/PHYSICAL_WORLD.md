# Evolit 0.0.7 Physical World

0.0.7 extends the Godot-independent `Evolit.Core` physical environment. Dependency direction remains `Evolit (Godot) -> Evolit.Core`.

## Layers

Geology is the slow foundation: elevation, geological substrate, mineral potential and geothermal potential. Geological substrate is not biological soil. `SubstrateDevelopment` represents the separate foundation for later weathering/soil formation.

Hydrology uses water depth, elevation, neighboring topology, deterministic downhill runoff, evaporation and precipitation. It is a cheap cell model, not CFD. Existing 0.0.5 rivers/lakes seed Core water state through the Godot adapter.

Atmosphere contains pressure, humidity and derived wind components. Pressure relaxes toward an elevation-dependent target; pressure gradients produce the wind foundation.

Climate temperature is derived from a global axial latitude proxy, elevation, water moderation, local neighbor diffusion and simulation time. Humidity is coupled to water availability, evaporation, precipitation and neighboring humidity. Light follows simulation time and local humidity/water attenuation.

Resources remain compact fields: mineral potential, nutrient potential, organic matter, water availability and substrate development. Nutrients respond to mineral potential, water/weathering and organic matter.

## Update pipeline

All updates use `WorldTopology`; systems never require exactly six neighbors. Rates are centralized in `EnvironmentSchedule`. Hot environment fields are dense arrays. Reusable scratch buffers prevent order-dependent in-place diffusion and avoid per-tick arrays.

Live and Bootstrap use the same systems. Bootstrap increases relaxation rates rather than creating a second simulation engine. The headless runner can replay bootstrap and report a stabilization change metric.

## Derived regions

**Biome is derived, not canonical state.** `EnvironmentRegion` is computed from current physical fields. It describes conditions such as `TemperateHumid`, `Arid` or `DeepWater`.

Forest, grassland, reef and other real ecosystems must emerge from actual life. The environment generator does not spawn them from a climate label.

## Persistence and determinism

Core snapshot version 2 persists wind, precipitation, evaporation, runoff and water availability in addition to the 0.0.6 fields. Version 1 Core snapshots remain accepted; missing physical fields are deterministically initialized from persisted water/humidity. Scheduler phase remains derivable from `TickCount` because all system intervals are fixed integer tick intervals.

The headless runner verifies full-snapshot deterministic replay, different-seed divergence, physical bounds, bootstrap replay and save/restore continuation. It also provides environment-only and existing combined organism benchmarks.

## Godot integration

`CoreSimulationHost` remains the bridge. The existing map seeds Core geology/water/climate state, while live simulation state belongs to Core. Holding `T` now reads the selected cell's current Core elevation/water, climate, wind, light, resources, substrate and derived region. The legacy renderer and demo UI remain intact for incremental migration.

## Scope boundary

0.0.7 does not implement plate tectonics, CFD, full weather, ocean currents, plants, animals, microbial evolution, AI, pathfinding, reproduction or speciation. It provides the deterministic physical conditions those later systems can consume.
