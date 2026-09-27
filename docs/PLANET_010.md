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
