# Evolit 0.1.0 3D Planet

Development branch: `feature/evolit-0.1.0-3d-planet`

Base 0.0.9 commit: `de12fb8fe1899421fa990c44423f9168f77c1e24`

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
- Existing `worldgen-verify`, `worldgen-quality`, `environment-verify` and benchmark commands remain available for Flat regression checks.

GitHub Actions are not part of the ordinary development verification path. Final release CI remains gated by explicit release approval.


### Final planet-generation refinements

The completed 0.1.0 pipeline also applies these spherical-specific rules:

- Macro geography is driven by 4–5 deterministic, strongly separated continental anchors combined with pseudo-plate geology and seamless 3D domain-warped noise. This reduces accidental single-supercontinent worlds while still allowing landmasses to merge naturally at higher land settings.
- The global ocean is classified from major connected below-sea-level components. Small disconnected negative basins are retained as inland seas/lakes instead of being treated as implicit world-edge ocean.
- Priority-flood drainage starts from the classified ocean graph. Inland depressions receive deterministic spill routes; tiny shallow lake noise is pruned by raising the terrain to its spill surface, while genuine below-sea-level inland basins remain water.
- River validation checks direct-neighbor drainage, cycle freedom, hydraulic direction, downstream flow accumulation, nondecreasing river width and nondecreasing stream order.
- Dual-cell polygon corners use spherical triangle circumcenters rather than normalized triangle centroids, improving Voronoi boundaries around ordinary hexagons and the 12 pentagons.
- Physical bathymetric terrain, visible ocean/lake water, rivers, grid, selection and entity markers are separate batched meshes. Adjacent terrain cells share corner elevations, removing the previous pyramid/crack failure mode.
- Planet entities are rendered through one batched marker mesh instead of one `MeshInstance3D` per entity.
- LOD hides cell grid, entity markers and finally rivers as the camera moves away, leaving a clean global planet view.
- Core environment snapshots persist the generated climate baseline and target pressure, preserving deterministic continued simulation across Planet save/load.
