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
The edge falloff uses hex distance, matching the actual playable boundary. Volcanic
island arcs cannot spawn on the outer three cells. A bounded deterministic
candidate search also favors worlds without one overwhelming landmass.

## Relief

Mountain systems are tied to convergent pseudo-plate boundaries instead of arbitrary isolated blobs. Uplift has broad halos, while a limited thermal-style relaxation pass turns isolated spikes into connected peaks, foothills and highlands.

Stable interiors are intentionally smoother so large plains remain possible. Divergent zones provide valley/rift foundations. Rivers later carve shallow corridors into the final relief.
After sea-level selection, positive macro elevation is compressed before
tectonic relief is applied. This keeps broad continental interiors low while
plate convergence supplies most prominent high ground.

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
Each land cell selects the steepest neighbor with a strictly lower filled
hydraulic surface; the priority-flood parent remains a safe fallback. A lake
must cover at least two cells, so single-cell sinks are filled into terrain.

## Initial climate

Initial temperature still depends on latitude, elevation and climate preset.

Humidity uses real water distance plus a deterministic west-to-east moisture transport pass. Tectonic uplift and local slope remove moisture to create an initial rain-shadow approximation. Runtime climate remains owned by the existing Core environment systems.

## Determinism and RNG separation

Subsystems use separate SeedMixer-derived streams for macro geography, geology, islands, erosion/hydrology detail and climate. Same version + seed + settings + size produces the same starting world.

Runtime generation may evaluate a small bounded number of deterministic candidates and choose the best quality score. Aesthetic quality thresholds never make New Game crash.
The current bound is twelve candidates. This is a worst-case cap, not the
normal number of generation passes.

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


## Second refinement pass

The second 0.0.9 refinement pass keeps the existing pseudo-plate / priority-flood architecture and targets the remaining visual audit findings rather than replacing the generator.

### River geometry

Priority flood still supplies the safe hydraulic escape surface. A deterministic drainage refinement now evaluates only strictly lower hydraulic neighbours and keeps hydraulic drop as the dominant score. When multiple physically reasonable alternatives exist, a small downstream-direction preference discourages long ruler-straight hex runs without forcing alternating zig-zags. A tiny seed-derived tie-breaker is used only inside that physically valid candidate set.

Generation now records Strahler-style stream order, upstream branch count and downstream hex direction. Visible channels require meaningful accumulation/length, while width combines catchment flow and stream order. High-order coastal mouths receive a small width increase as a generation-only delta foundation.

### Coast anti-grid shaping

Near sea level, an anisotropic continuous-space field is sampled in a province-specific rotated coordinate frame before land/water compression. It changes broad shoreline curvature, bays and peninsulas at angles independent of the six hex axes. A deterministic topology pass then removes isolated one-cell water holes and selected one-cell land bridges while leaving larger bays, straits and islands intact.

### Macro variety

Pseudo-plate layouts now vary across 10–15 total plates and 3–6 continental plates with stronger deterministic position/crust variation. A seed-derived fragmentation style modifies continental-boundary rifting instead of requesting a continent count directly. Candidate scoring prefers useful 3–4-landmass compositions while keeping 2-continent and rarer layouts valid.

### Terrain readability

Bare rock and basalt generation thresholds are stricter. Gentle terrain above the lowlands is presented as a derived Highland terrain instead of being classified as Rocky solely from elevation. Existing terrain enum numeric values are preserved by appending Highland, so older saved terrain values remain stable.

### Quality diagnostics

World-generation quality now additionally reports:

- longest / mean straight river run and fraction in long runs;
- longest / mean same-axis coast run and fraction in long runs;
- plain-like / rocky / mountain land ratios;
- inland-sea count and largest inland sea;
- deterministic candidate attempt and whether the preferred threshold was accepted.

`worldgen-quality [count]` supports larger fixed samples such as 50 seeds and reports a continent histogram plus candidate-search statistics. `worldgen-gallery [directory] [count]` defaults to 15 deterministic Medium worlds.

### Candidate memory

Rejected generation candidates no longer retain a fully materialized EnvironmentStore, and the generator no longer keeps a complete best world alive while generating another candidate. Candidate selection retains only best seed/score metadata; the selected candidate is materialized into the canonical EnvironmentStore only once. The pre-carving and post-carving hydrology passes also reuse the same dense result buffers instead of retaining two complete hydrology result sets. The final generation timing includes the full candidate search plus final environment materialization.

### Compatibility

Save-map diagnostics now include stream order, branch count and river direction. Missing fields in older schema-1 / 0.0.8 JSON use safe defaults, and Highland was appended to the terrain enum rather than inserted into existing numeric values.

The refinement does not change world radii, runtime environment scheduling, the batched terrain renderer architecture, biological scope, or the flat-hex world model.


## Visual river surface pass

The generated hydrology remains cell-based and deterministic, but the presentation no longer needs to paint an entire land hex blue just because a river crosses it.

New worlds keep `WaterKind.River` for hydrology/movement while retaining the physical land terrain classification underneath. Each visible terrain chunk builds one cached river `ArrayMesh` at configure/load time. Every river cell contributes a short quadratic ribbon toward its downstream neighbour using deterministic sub-cell anchor offsets and a small curvature term. Tributaries therefore meet through shared downstream anchors, channel width still comes from generated `RiverWidth`, and the river overlay adds at most one extra base draw call per visible chunk.

This is presentation-only. Drainage, stream order, flow accumulation, basin ids and physical water depth stay unchanged. Debug generation layers hide the overlay so scalar fields remain readable.

Legacy saves without `RiverDirection` keep their old full-hex River terrain instead of silently losing visible rivers. Saves that contain 0.0.9 route metadata are normalized to land terrain plus the cached overlay on restore.
