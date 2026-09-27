# Evolit 0.0.9 generation audit

Date: 2026-09-26. Scope: the `feature/evolit-0.0.9-world-generation`
development branch. The 0.0.8 checkpoint is
`9a5d0f98cdf8921d674740a83cd2cd4e2e2202ff`. This audit uses local
headless runs and actual Godot 4.4.1 Mono rendering; it is not a release audit.

## Starting state and root causes

The branch already contained a substantial 0.0.9 plate, relief, priority-flood,
climate, save and diagnostics pipeline. It did not build: eleven C# errors came
from ambiguous `Input`, missing `UiMetrics` members, a static drawing method,
and ambiguous `FileAccess`. The committed PNG icon was truncated.

The 0.0.8 generator used warped elliptical continental anchors. In the 0.0.9
branch, weakly blended nearest-plate crust still made near-straight region
borders; a circular boundary falloff was too weak along the flat hex sides.
Positive macro elevation made large parts of the map render as rocky gray.
Priority-flood first-discovery routes formed spokes, and single deep cells
could become lakes. Before the corrections in the original local checkout,
`worldgen-quality` failed 13 of 20 seed cases, mostly because land reached the
map boundary. During this audit, the remote branch was squashed at `256fc2a`;
the final changes were integrated on top of that new head. Its added
continental-shelf islands and per-province coast style were retained.

## Generation path and audit A–X

| Area | Finding |
|---|---|
| A–D Macro, continents, connectivity, ocean | Pseudo-plates plus warped broad and regional fields, sea level, component cleanup and bounded candidate selection make 2–3 major landmasses in the measured set. Ocean is connected to the full outer boundary in all 20 cases. |
| E–G Bathymetry, coasts, islands | Coast-distance BFS drives shelf/deep-water transition. Land has bays, straits, peninsulas, continental-shelf islands and volcanic arcs. Hex-distance edge taper and arc exclusion protect the boundary. Several coasts still show long segments parallel to hex edges. |
| H–L Mountains, highlands, plains, valleys, slopes | Convergent boundaries drive uplift; stable interiors retain broad plains after positive elevation compression. Slope and shallow river carving are present. Actual renderer classification now leaves more interior land green. Some gray uplands still read as broad patches. |
| M–Q Depressions, drainage, rivers, basins, lakes | Priority flood resolves sinks. Acyclic steepest-filled-surface routing feeds flow accumulation and catchments. Rivers have connected systems and tributaries; one-cell lakes are removed and larger basins retain spill routes. Some river runs remain visibly straight or parallel. |
| R–S Climate and geology | Generation-only west-to-east moisture transport, elevation temperature and rain-shadow approximation use the new relief; plate convergence/divergence informs uplift, rifts and volcanic arcs. No new runtime climate or biology system was added. |
| T–U Determinism and variety | `worldgen-verify` and 20-seed checks passed. Ten same-zoom Godot overview images have differing ocean and land layouts; two-continent outcomes still dominate. |
| V Save/load | Godot UI New Game with selected Medium, High land, Warm climate and Active geology created a manual save containing the map and Core snapshot. Load and repeated load from the Save screen passed. Invalid enum values fall back safely. A JSON round-trip preserved a changed elevation marker, province and basin. |
| W Performance | One-shot generation benchmark: 285/254/715 ms for Small/Medium/Large versus 558/695/1172 ms from a clean 0.0.8 checkpoint on this machine. Bootstrap timings varied across repeated runs on both revisions. Large `memory_delta` is 121.5 MB versus 63.4 MB; this metric includes one-shot retained allocations and is not peak RAM. In-game comparative FPS was not measured. |
| X Visual quality | Ten rendered Medium worlds were reviewed. Oceans, bays, island groups and green plains are readable. Straight river segments, some flat coast runs and broad gray uplands remain. The visual result is improved but not uniformly natural. |

## Quantitative 20-seed sample

All rows use Medium radius 76 (`17,557` hexes) and default settings. Land and
largest-continent columns are percentages. `Visual result` refers to these
exact `quality-*` seeds; only the separate `gallery-*` set was visually reviewed.

| Seed | Continents | Land % | Largest continent % | Islands | Mountain ranges | Rivers | Longest river | Lakes | Visual result |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| quality-00 | 2 | 41.0 | 80.0 | 8 | 2 | 24 | 66 | 10 | NOT REVIEWED |
| quality-01 | 2 | 40.3 | 49.3 | 6 | 5 | 25 | 42 | 11 | NOT REVIEWED |
| quality-02 | 2 | 41.3 | 52.2 | 6 | 9 | 21 | 47 | 10 | NOT REVIEWED |
| quality-03 | 3 | 40.9 | 58.5 | 9 | 4 | 32 | 49 | 8 | NOT REVIEWED |
| quality-04 | 2 | 40.8 | 49.0 | 7 | 7 | 22 | 68 | 12 | NOT REVIEWED |
| quality-05 | 2 | 40.8 | 72.3 | 4 | 6 | 26 | 72 | 5 | NOT REVIEWED |
| quality-06 | 2 | 41.1 | 62.3 | 12 | 9 | 30 | 55 | 5 | NOT REVIEWED |
| quality-07 | 2 | 40.5 | 72.2 | 2 | 9 | 30 | 60 | 6 | NOT REVIEWED |
| quality-08 | 2 | 41.2 | 75.0 | 5 | 8 | 31 | 58 | 7 | NOT REVIEWED |
| quality-09 | 2 | 40.5 | 63.2 | 4 | 6 | 28 | 43 | 3 | NOT REVIEWED |
| quality-10 | 3 | 41.6 | 41.1 | 10 | 11 | 27 | 54 | 9 | NOT REVIEWED |
| quality-11 | 2 | 41.6 | 82.0 | 3 | 13 | 32 | 65 | 14 | NOT REVIEWED |
| quality-12 | 2 | 40.1 | 60.1 | 2 | 8 | 42 | 57 | 11 | NOT REVIEWED |
| quality-13 | 3 | 41.2 | 34.7 | 12 | 10 | 29 | 37 | 6 | NOT REVIEWED |
| quality-14 | 2 | 40.5 | 58.7 | 4 | 8 | 23 | 39 | 2 | NOT REVIEWED |
| quality-15 | 2 | 40.5 | 65.5 | 5 | 10 | 28 | 40 | 8 | NOT REVIEWED |
| quality-16 | 2 | 42.2 | 88.1 | 8 | 11 | 31 | 51 | 16 | NOT REVIEWED |
| quality-17 | 2 | 40.8 | 65.6 | 2 | 10 | 27 | 41 | 9 | NOT REVIEWED |
| quality-18 | 2 | 41.2 | 76.3 | 6 | 10 | 24 | 69 | 15 | NOT REVIEWED |
| quality-19 | 2 | 40.5 | 66.4 | 4 | 11 | 29 | 51 | 4 | NOT REVIEWED |

Result: `WORLDGEN QUALITY PASS seeds=20`. There are 17 two-continent and three
three-continent worlds. Land covers 40.1–42.2%. The largest continent spans
34.7–88.1% of land. All 20 report 100% outer-boundary ocean. The retry limit is
twelve; rare layouts can use the best candidate even when the preferred 84%
largest-land threshold was not met. The headless hard-fail limit is 92%.

## Visual 10-seed review

The ten `gallery-00` through `gallery-09` screenshots came from the actual
Godot `DemoWorldView`, with Medium size and a common overview zoom. They were
kept outside Git. `PASS with caveat` means the basic geographic composition is
readable while the listed visual issue remains.

| Seed | Visual result | Observed issue |
|---|---|---|
| gallery-00 | PASS with caveat | Long northern/southern coast runs and straight river reaches |
| gallery-01 | PASS with caveat | Broad gray uplands and some edge-aligned coast |
| gallery-02 | PASS with caveat | Elongated joined landmass and straight river sections |
| gallery-03 | PASS with caveat | Several parallel river reaches and flat northern coast |
| gallery-04 | PASS with caveat | Western shore follows the hex edge in places |
| gallery-05 | PASS with caveat | Eastern island is simple in outline |
| gallery-06 | PASS with caveat | Large connected landmass and broad gray region |
| gallery-07 | PASS with caveat | Narrow link and straight river reaches |
| gallery-08 | PASS with caveat | Southern and northern coast segments remain flat |
| gallery-09 | PASS with caveat | River segments remain geometric |

F4 generation layer and T inspector (including Core environment and generation
metadata) were exercised in Godot. Camera zoom, center and reset passed. The
rendered UI New Game flow, manual save, load and repeated load passed in an
isolated `Evolit-009-Sync-QA` user directory. This did not touch normal player saves.

## Verification, changed code and remaining risks

Local checks: `dotnet restore`, `dotnet build`, `verify`,
`environment-verify`, `worldgen-verify`, `worldgen-seeds`,
`worldgen-quality`, `worldgen-benchmark`, `worldgen-gallery`, Godot editor
import/start and the scene tests above. The temporary scene harnesses and
screenshots are excluded from the branch commit.

Changed code: `WorldGeneration.cs`, `WorldGeneration009.cs`,
`WorldMapGenerator.cs`, `GameSession.cs`, `SaveManager.cs`,
`DemoWorldView.cs`, `SpeciesPortrait.cs`, the headless output,
and the repaired branding PNG. The existing branch had already updated
`AppVersionCatalog` and `versions.json` to 0.0.9.

P0/P1 found in the original checkout audit (build and load failures, 13/20 quality failures):
fixed and verified. Remaining P2: geometric river reaches, occasional edge
alignment, broad gray uplands, low frequency of four-continent layouts, and the
large one-shot memory delta. In-game 0.0.8/0.0.9 FPS comparison and older
0.0.8 save-file UI migration were not performed. No GitHub Actions, merge,
tag or release was run as part of this audit.


## 2026-09-27 second refinement implementation

Starting checkpoint for this pass: `7e7b85a2c7bb5a93a99cd990a8ef252f7094961e`.

The implementation addresses the P2 findings recorded above: river straightness,
hex-axis coastline runs, low macro-layout variety, broad gray uplands and
candidate-search memory pressure. It adds deterministic hydraulic routing
preferences, stream-order metadata, continuous-space coast curvature, selected
one-cell bridge cleanup, wider pseudo-plate/crust variation, rift-style
fragmentation bias, derived highlands, river/coast geometry metrics, 50-seed
quality tooling and deferred EnvironmentStore materialization.

Verification status in the environment that produced this commit:

- `dotnet restore`: NOT RUN — .NET SDK unavailable.
- `dotnet build`: NOT RUN — .NET SDK unavailable.
- `verify`: NOT RUN.
- `environment-verify`: NOT RUN.
- `worldgen-verify`: NOT RUN.
- `worldgen-seeds`: NOT RUN.
- `worldgen-quality 20/50`: NOT RUN.
- `worldgen-benchmark`: NOT RUN.
- `worldgen-gallery`: NOT RUN.
- Godot visual review/screenshots: NOT RUN — Godot unavailable.
- FPS/TPS comparison: NOT MEASURED.
- 0.0.8 save UI migration: NOT RUN.

Static repository review confirmed balanced changed C# source delimiters, preserved
0.0.9 version scope, append-only terrain enum compatibility and no change to the
manual-only GitHub Actions trigger. Runtime P0/P1 status must therefore remain
unverified until the local .NET/Godot suite above is run; no PASS metrics are
claimed by this appendix.


## 2026-09-27 visual realism pass — implementation checkpoint

Starting checkpoint: `45e9036342267897bfc6ecea8da4e57df3341b75`.

The repository audit confirmed an important presentation-level cause of geometric
rivers: generated river cells were still classified and colored as full
`HexTerrainType.River` hexes. Even a better drainage path therefore remained
visually locked to the six-cell grid.

This checkpoint separates river hydrology from terrain presentation:

- new generated river cells keep their physical land terrain classification;
- `WaterKind.River` remains authoritative for water/movement semantics;
- each base terrain chunk caches one additional batched `ArrayMesh` containing
  curved river ribbons;
- ribbon endpoints use deterministic sub-cell anchors shared by adjacent cells,
  so tributaries connect while no river geometry is rebuilt per frame;
- width continues to come from generated `RiverWidth`;
- generation debug layers hide the river overlay;
- legacy saves without route-direction metadata keep their old full-hex river
  rendering so old rivers do not disappear during migration.

Static checks in this environment found balanced changed C# source delimiters
and the workflow remains manual-only. The required Godot screenshot gate from
the visual-pass specification cannot be completed here because this execution
environment has no .NET SDK, Mono C# compiler or Godot executable, and direct
GitHub network access from the local shell is unavailable.

Therefore the mandatory visual classifications remain:

- `visual-00..19 BEFORE`: NOT REVIEWED in this environment;
- `visual-00..19 AFTER`: NOT REVIEWED;
- `unseen-00..19 AFTER`: NOT REVIEWED;
- Small/Large screenshot set: NOT REVIEWED;
- FPS/TPS: NOT MEASURED;
- old 0.0.8 save UI migration: NOT RUN.

No visual PASS is claimed by this checkpoint.


## 2026-09-27 continuous geography architecture checkpoint

Starting HEAD: `efc4608046e7d05e60c61750b3103f68cad9857a`.

The active 0.0.9 generator has been restructured so macro geography is built on a temporary higher-resolution continuous field and then sampled into the existing hex simulation topology. The previous active per-hex macroplate implementation was removed. Core topology, priority flood, drainage, stream order, EnvironmentStore and simulation ownership remain hex-based.

Implemented at this checkpoint:
- deterministic seed-derived `WorldGeographyStyle`;
- geological regions grouped under macroplates;
- continuous crust/elevation/tectonic fields;
- continuous coast contour extraction;
- convergent macroplate ridge centerline extraction;
- continuous island-margin / oceanic-arc contributions;
- continuous-field sampling into physical hex cells;
- coast + ridge + river cached chunk meshes;
- presentation save/restore without physical regeneration;
- old-save empty-presentation fallback;
- geometry determinism checks;
- geometry and resolution benchmark commands;
- renderer geometry diagnostics.

External source policy: no third-party code was copied. The reviewed Red Blob projects are Apache-2.0, Azgaar and WorldEngine are MIT, and vnovak404/worldgen has no declared license found in GitHub metadata / repository LICENSE lookup, so it was treated strictly as conceptual research.

Runtime verification in this execution environment remains blocked because `dotnet`, `godot`, `godot4`, `csc` and `mcs` are unavailable.

Therefore:
- dotnet restore/build: NOT RUN;
- verify/environment-verify/worldgen-verify: NOT RUN;
- worldgen-quality 50: NOT RUN;
- worldgen-benchmark: NOT RUN;
- worldgen-resolution-benchmark: NOT RUN;
- worldgen-geometry: NOT RUN;
- Godot BEFORE/AFTER/unseen visual audit: NOT REVIEWED;
- Medium/Large FPS and TPS: NOT MEASURED;
- 0.0.8 / early-0.0.9 UI save migration: NOT RUN.

The continuous architecture is therefore an implementation checkpoint, not a completed visual acceptance pass. Definition-of-done items that require real screenshots, 16/20 unseen PASS, build/runtime tests and measured performance remain open.


### Final static audit for continuous-geography checkpoint

Static repository checks after the implementation:
- all changed C# files have balanced braces/parentheses/brackets;
- `GeneratedWorldCell` record and its generation constructor both contain 28 positional fields;
- the active 0.0.9 pipeline has zero `BuildPlates`, `GenerateMacroGeography` or legacy `Plate[]` references;
- exactly one continuous-geography build path feeds the active generator;
- new region/macroplate/boundary metadata is wired through generation → Godot map → save → restore;
- continuous presentation state is persisted/restored without physical world regeneration;
- deterministic headless equality now includes both new cell metadata and coast/ridge presentation geometry;
- `Random.Shared`, wall-clock time and GUIDs are not used by the continuous generator;
- application version remains `0.0.9`;
- the repository workflow remains `workflow_dispatch` only.

Compiler/runtime status is unchanged: the local execution environment has no .NET SDK/C# compiler/Godot executable, so this checkpoint is not labeled build-pass or visual-pass.
