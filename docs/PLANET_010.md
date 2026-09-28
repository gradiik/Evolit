# Evolit 0.1.0 3D Planet

Development branch: `feature/evolit-0.1.0-3d-planet`

Original 0.0.9 branch point: `de12fb8fe1899421fa990c44423f9168f77c1e24`

Synchronized flat-world checkpoint: `2f25e0acfd5ee3fc9906b1a872a575058fc5dedf`

## World shapes

Evolit 0.1.0 keeps the existing flat axial-hex world as the default and adds `WorldShape.Planet`.

Flat worlds continue to use `WorldMap`, `HexCoord` and `DemoWorldView`.

Planet worlds use `PlanetWorldMap`, a Godot-independent geodesic topology in Evolit.Core and the dedicated `PlanetWorldView` presentation layer.

## Spherical topology

The planet surface is generated from a frequency-subdivided icosahedron. The primal triangulation is converted to a dual cell graph:

- every primal vertex becomes one planet cell;
- exactly 12 cells have five neighbors;
- all other cells have six neighbors;
- the graph is closed and has no boundary, pole hole or longitude seam;
- cell IDs use `CellId.FromPlanetIndex` and remain deterministic for a given frequency;
- surface directions and dual polygon corners are stored with `CoreVector3`, so Evolit.Core has no Godot dependency.

Planet resolutions intentionally track the current flat map cell counts:

| Size | Frequency | Planet cells |
|---|---:|---:|
| Маленький | 31 | 9,612 |
| Средний | 42 | 17,642 |
| Большой | 54 | 29,162 |

## Generation

Planet generation reuses the 0.0.9 data model but replaces flat assumptions with spherical operations.

Macro geography uses deterministic pseudo plates and seamless 3D value-noise/FBM sampled from the normalized sphere direction. There is no UV noise and therefore no longitude seam.

The generated environment includes elevation, bathymetry, coast distance, tectonic uplift, slope, substrate, geothermal potential, drainage, flow accumulation, basins, rivers, lakes, temperature, humidity, pressure, mineral potential and nutrient potential.

Hydrology follows topology neighbors on the closed graph. Oceans are defined by sea level instead of map-boundary contact.

Climate latitude is derived from the Y component of the normalized sphere direction. Runtime atmosphere also uses planet surface directions rather than axial Q/R deltas.

## Renderer

`PlanetWorldView` is separate from the existing flat renderer.

The planet uses one batched terrain `ArrayMesh`, one optional grid mesh, one selection mesh, one camera and one light. It does not create one Godot node or collider per cell.

Far view hides cell boundaries. Near view enables the grid. Terrain inspector remains on T and world-generation debug visualization remains on F4.

Picking projects a camera ray onto the planet sphere, converts the hit point to a normalized direction and resolves the nearest cell through a spatial bucket lookup. It cannot select cells through the back side of the planet.

## Save compatibility

`SaveDocument.WorldShape` defaults to Flat, so schema-1 saves created before 0.1.0 remain compatible.

Planet saves persist planet frequency, cell environmental/world-generation data, entity surface cell IDs and the Evolit Core snapshot. Core topology snapshots persist topology kind and surface directions.

The in-memory camera view state now distinguishes flat camera state from planet orbit state.

## Verification

The headless verification suite includes planet checks for:

- expected geodesic cell count;
- unique stable IDs;
- exactly 12 pentagons;
- degree 5/6 only;
- no self or duplicate neighbors;
- mutual adjacency;
- connected graph;
- normalized sphere positions;
- polygon winding;
- same-seed determinism;
- different-seed environment variation;
- Core snapshot round trip with planet topology.


## Completion pass

The post-foundation 0.1.0 pass keeps the original geodesic topology but tightens the full gameplay path.

- Compatible 0.0.9 world-generation improvements are carried into the flat mode: physical terrain remains underneath rivers, `Highland` terrain is preserved, stream order/upstream/direction metadata is saved, and flat rivers use the cached batched ribbon presentation.
- Planet macro geography samples seamless 3D noise through deterministic spherical domain warp and graph smoothing instead of adding any UV or map-edge logic.
- Planet climate keeps the selected generated climate as the runtime baseline, while atmosphere transport continues to use the local tangent basis.
- Closed-sphere priority-flood drainage uses deterministic priority ordering and validates direct-neighbor flow, downstream accumulation, uphill flow and cycles.
- Planet terrain relief now shares corner elevations between adjacent dual cells. Terrain bathymetry is a physical surface; ocean/lake water is a separate batched water mesh; rivers are a separate batched spherical ribbon mesh.
- River cells keep their underlying land terrain and movement rules. A river is hydrology metadata/presentation, not a blue replacement terrain polygon.
- Planet save state persists drainage/river metadata plus orbit camera, distance, selected entity and selected cell. Missing view data remains compatible with older schema-1 saves.
- Picking first searches nearby spatial buckets before the guarded full-scan fallback.

### Local verification entry points

The headless executable exposes:

- `verify` — full Core verification including Planet verification.
- `planet-verify` — topology, deterministic generation, physical/hydrology validation, Large topology construction and Core snapshot round trip.
- `planet-seeds [count]` — deterministic Medium-planet seed sweep, default 20.
- `planet-benchmark` — Small/Medium/Large generation timings, allocations and memory deltas.
- Exported-app render capture: `--capture-planet=<png> --seed=<seed> [--zoom-steps=0..6] [--orbit-yaw-degrees=<degrees>]`; captures use temporary isolated saves and never write to the user's save slots.
- Existing `worldgen-verify`, `worldgen-quality`, `environment-verify` and benchmark commands remain available for Flat regression checks.

GitHub Actions are not part of the ordinary development verification path. Final release CI remains gated by explicit release approval.


### Final planet-generation refinements

The completed 0.1.0 pipeline also applies these spherical-specific rules:

- Macro geography uses one to three deterministic, strongly separated continental anchors combined with pseudo-plate geology and seamless 3D domain-warped noise. Medium worlds are quality-checked across fixed seeds for 1–3 connected major landmasses; small isolated volcanic island groups are added along tectonically active offshore regions.
- Sea level is 0 m. Low positive land relief starts at roughly 1.5 m, broad hills rise gradually, and plate uplift supplies higher terrain. Ocean depth transitions through a broad shallow shelf before deep-basin relief; the surface colors are interpolated across adjacent same-medium cells to reduce speckle without changing cell ownership or hydrology.
- The global ocean is classified from major connected below-sea-level components. Small disconnected negative basins are retained as inland seas/lakes instead of being treated as implicit world-edge ocean.
- Priority-flood drainage starts from the classified ocean graph. Inland depressions receive deterministic spill routes; tiny/shallow basins are raised to their spill surface, while below-sea-level inland seas remain water. If lake coverage exceeds 20% of land, the shallowest above-sea-level lake systems are deterministically filled first.
- River cells have positive water depth and remain `HexWaterKind.River` water cells in the simulation map, not only a decorative surface line. Save/load verification checks that river-water habitat cells survive the round trip.
- River validation checks direct-neighbor drainage, cycle freedom, hydraulic direction, downstream flow accumulation, nondecreasing river width and nondecreasing stream order.
- Dual-cell polygon corners use spherical triangle circumcenters rather than normalized triangle centroids, improving Voronoi boundaries around ordinary hexagons and the 12 pentagons.
- Physical bathymetric terrain, visible ocean/lake water, rivers, grid, selection and entity markers are separate batched meshes. Adjacent terrain cells share corner elevations, removing the previous pyramid/crack failure mode.
- Coastal land starts just above sea level and the classifier renders low near-shore cells as sand; the fixed-seed runtime capture records both 0–2 m and 1–2 m beach counts.
- The default overview and camera reset center the largest connected landmass. Deep-ocean colors have a brighter blue floor, while same-medium terrain color blending also softens cell centers to reduce close-view speckle without blurring the land/water boundary.
- Planet entities are rendered through one batched marker mesh instead of one `MeshInstance3D` per entity.
- LOD hides cell grid, entity markers and finally rivers as the camera moves away, leaving a clean global planet view.
- Core environment snapshots persist the generated climate baseline and target pressure, preserving deterministic continued simulation across Planet save/load.


## Post-audit picking and polar rendering hardening

The planet cell spatial lookup now evaluates the complete local bucket neighbourhood before accepting a nearest-cell result. This avoids incorrect selection near quantization boundaries while keeping normal picking bounded to a small local candidate set instead of an O(N) scan.

Planet river ribbon tangent construction is also pole-safe: when the projected downstream tangent degenerates, the renderer chooses a reference axis that is not parallel to the local radial direction. This prevents zero-width/invalid river geometry near the poles.


## Stabilization and physical-scale pass

The 0.1.0 branch synchronizes the compatible flat-world implementation from the current 0.0.9 checkpoint without merging the two development branches.

Physical vertical semantics are shared by Flat and Planet:

- sea level is exactly 0 m;
- land elevation is positive;
- seabed elevation is negative;
- water depth is a positive water-column thickness;
- terrain classification uses absolute elevation together with local slope, two-ring local relief, tectonic uplift and substrate.

The largest flat preset is `Очень большой` with radius 170 and exactly 87,211 axial cells. Planet Huge remains disabled until a real renderer/topology benchmark proves it usable.

Flat rivers remain hydrology overlays over physical terrain. The cached chunk river mesh now uses a deterministic sub-cell junction per hex, a shared edge crossing and two curved segments so tributaries meet instead of drawing centre-to-centre sticks.

Planet presentation uses `PlanetVisualScale` as the single physical-metres-to-visual-radius conversion for terrain, water, rivers, grid, selection and markers. Physical elevation is never modified for presentation. Relief is visually exaggerated uniformly on the radius-3 globe.

Planet river meshes are cached in three LOD batches. Their centreline follows spherical interpolation, meander is applied in the tangent plane, and every sample is projected onto the visible terrain/water surface before a minimal z-fighting offset is added.

The planet camera zoom changes camera distance only. Minimum distance is derived from the maximum visual surface radius, the camera near plane and a safety margin. Grid and river LOD visibility changes do not rebuild meshes.

The planet scene uses ambient environment light in addition to the directional light so the shadow hemisphere remains readable. F6 cycles render diagnostics for unshaded terrain, double-sided terrain, normal visualization, edge overlay, terrain-only, water-only, rivers-only and grid-only views.

Terrain receives a bounded vertex-color light gradient with a high ambient floor, so the night-side geography remains visible while the globe retains directional shape. Planet HUD metric captions and values use larger sizes at the standard 1280×720 window.

Terrain and water triangle construction validates finite vertices, non-zero area, outward winding and bounded edge length before a mesh is accepted. Headless topology verification additionally rejects invalid or giant polygon edges.

Planet surface, water and marker triangles follow Godot's clockwise front-face winding, so back-face culling keeps the camera-facing shell opaque and occludes the far hemisphere.

### Added local tools

- `worldsize-benchmark` measures candidate radii 98, 125, 140, 155 and 170 and prints cells, generation time, allocations, memory delta and bootstrap timings.
- `planet-benchmark` also prints generated physical elevation/depth extrema.
- `planet-verify` validates local relief and spherical geological metadata in addition to the existing topology/hydrology checks.

These tools are local verification entry points. GitHub Actions remain disabled for ordinary development.
