# Evolit 0.0.9 World Generation Rework

0.0.9 is dedicated to procedural world generation quality. It keeps the flat finite hex world, the 56 / 76 / 98 radii, and the runtime performance architecture from 0.0.8 Optimization Pass 4.

## Why 0.0.8 was not enough

The 0.0.8 quality pass still built macro geography from a small set of warped elliptical continental anchors. That reduced sponge-like maps but left recognizable blob shapes, weak seed variety, mountain belts that were only loosely related to geology, and naive local-minimum hydrology.

0.0.9 removes those anchors from the active pipeline. The old implementation is not kept as a parallel generator.

## Pipeline

The 0.0.9 generation flow is:

seed/settings
→ stable hex topology
→ pseudo-plate layout
→ plate crust + multi-scale macro fields
→ global sea-level selection
→ deterministic land/water component cleanup
→ pseudo-tectonic uplift/rifts/plateaus
→ volcanic island arcs
→ slope relaxation
→ coast-distance field
→ shelf/deep-ocean bathymetry
→ geology/substrate
→ priority-flood depression resolution
→ drainage tree + catchments
→ flow accumulation
→ river network + width
→ river-valley carving
→ final priority-flood hydrology
→ multi-cell lakes + spill/outlet paths
→ initial climate/moisture transport
→ minerals/nutrients
→ canonical EnvironmentStore
→ existing Core bootstrap
→ live simulation.

Noise is subordinate to the larger plate/crust structure. Local detail is not allowed to define the continental layout by itself.

## Pseudo-plates

The generator creates a deterministic set of geological provinces. Each province has a seed-controlled center, crust type, motion vector and volcanism factor.

Nearest/second-nearest province relationships form approximate plate boundaries. Relative motion produces convergence and divergence fields:

- convergence drives mountain uplift and some volcanism;
- divergence lowers rift-like corridors;
- stable continental interiors favor plains and broad plateaus;
- oceanic convergent boundaries can produce small volcanic island chains.

This is generation-time approximation only. There is no runtime plate tectonics simulation.

## Coastlines and oceans

Land amount remains controlled by the requested setting, but the underlying scalar field comes from plate crust plus global/continental/regional/local scales.

After sea-level selection, deterministic connected-component cleanup removes only obvious noise fragments and tiny enclosed water holes.

A multi-source BFS builds distance-to-coast in O(N). Ocean depth then grows from shallow continental shelf toward deep basins with large-scale basin variation. The finite hex boundary is still forced toward ocean so it is not traced by land.

## Relief

Mountain systems are tied to convergent pseudo-plate boundaries instead of arbitrary isolated blobs. Uplift has broad halos, while a limited thermal-style relaxation pass turns isolated spikes into connected peaks, foothills and highlands.

Stable interiors are intentionally smoother so large plains remain possible. Divergent zones provide valley/rift foundations. Rivers later carve shallow corridors into the final relief.

## Hydrology

0.0.9 replaces naive lowest-neighbor sinks with deterministic priority-flood depression handling on the hex topology.

Ocean cells are drainage outlets. Priority flood computes a spill-compatible hydraulic surface and a drainage parent for every land cell. Tiny depressions are filled into terrain; significant connected depressions remain lake basins.

Drainage then provides:

- catchment / basin ids;
- flow accumulation;
- tributary structure;
- river-system components;
- river length and width proxies;
- lake spill/outlet paths.

The main river network is generated from accumulated catchment flow, not random drawing. Major river cells receive a small valley-carving pass, then hydrology is rebuilt against the carved relief.

## Initial climate

Initial temperature still depends on latitude, elevation and climate preset.

Humidity uses real water distance plus a deterministic west-to-east moisture transport pass. Tectonic uplift and local slope remove moisture to create an initial rain-shadow approximation. Runtime climate remains owned by the existing Core environment systems.

## Determinism and RNG separation

Subsystems use separate SeedMixer-derived streams for macro geography, geology, islands, erosion/hydrology detail and climate. Same version + seed + settings + size produces the same starting world.

Runtime generation may evaluate a small bounded number of deterministic candidates and choose the best quality score. Aesthetic quality thresholds never make New Game crash.

## Quality metrics

GeneratedWorld exposes quantitative diagnostics used by headless tooling:

- continent count;
- largest / second-largest landmass;
- tiny-island count;
- inland-water components;
- coastline edges and complexity;
- mountain-range components;
- river-system count;
- total river cells;
- longest river;
- tributary junctions;
- lake-system count and largest lake;
- drainage-basin count;
- ocean coverage at the finite boundary;
- mean slope.

Headless commands include:

- worldgen-verify
- worldgen-seeds
- worldgen-summary <seed>
- worldgen-benchmark
- worldgen-quality
- worldgen-gallery [directory]

worldgen-gallery writes ten Medium-size SVG overviews for fast manual seed review without adding them to the repository.

## Godot diagnostics

The T inspector keeps live Core values and adds generation metadata: province id, continentalness, uplift, coast distance, basin id and river length/width.

F4 cycles developer-only layers including continentalness, province, elevation, slope, coast distance, water depth, flow accumulation, basin, tectonic uplift, temperature, humidity, substrate, minerals and derived environment region.

## Save/load

0.0.9 saves the generated presentation map and canonical Core snapshot. Loading a current save restores actual state and does not rerun procedural generation.

New generation metadata is optional in JSON and therefore old 0.0.8 saves remain readable with default diagnostic values.

## Scope boundary

0.0.9 does not add life, biological biomes, AI, pathfinding, reproduction, speciation, food webs, a spherical planet, 3D terrain, GPU generation or runtime tectonics.

The next version must not start automatically after this work.