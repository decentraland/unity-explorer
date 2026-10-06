# Map capture: session handover

State of the `chore/map-capture-tool` work as of 2026-09-30, written so a new session on the
Windows machine can pick it up without the previous conversation. The operating guide for
actually running a capture is `docs/map-capture-handover.md`; this file is the context around it:
what exists, why it is shaped this way, what was verified, what is still open.

## Goal

Regenerate the client's satellite map of Genesis City (the 8x8 grid of 512 px JPEGs the minimap
downloads from `genesis-city/parcels`, branch `new-client-images`, `maps/lod-0/3/{i},{j}.jpg`)
from the explorer's own renderer, using abgen's ISS (Initial Scene State) LOD_0 for every scene,
with full rendering parity to the client. Windows only. Editor first, then a standalone build for
the full run.

## Where things are

- Branch `chore/map-capture-tool` of `decentraland/unity-explorer`, based on PR
  https://github.com/decentraland/unity-explorer/pull/10263 and merged with `dev` on 2026-09-29.
  Everything is pushed; head at the time of writing is `399b7b2a0`.
- Unity project root is `Explorer/`. Open Unity there, never at the repository root.
- The capture lives in `Explorer/Assets/DCL/Infrastructure/Global/MapCapture/` (namespace
  `Global.MapCapture`, assembly `DCL.Plugins` through the folder's asmref), the scene is
  `Explorer/Assets/Scenes/MapCapture.unity`, the build script is
  `Explorer/Assets/Editor/MapCaptureBuild.cs`, and the launch flags are the nested `MapCapture`
  class in `Explorer/Assets/DCL/Infrastructure/Global/AppArgs/AppArgsFlags.cs`.
- `scripts/abgen_coverage.py` walks the city against the catalyst and the abgen registry and
  reports scenes without an abgen manifest or ISS descriptor. Needs only Python 3, no packages.
- Analysis outputs from the Mac (not in the repo) are in `~/Downloads/` there:
  `abgen-missing.{json,csv}`, `abgen-rebuild.txt`, `abgen-rebuild-with-ids.csv`,
  `abgen-no-descriptor.txt`, `abgen-lod-failures.csv`. The script regenerates the first two.

## How the capture works

`MapCaptureSceneLoader` (MonoBehaviour on the `MapCapture` object) reads `MapCaptureArgs` from the
command line, or in the editor synthesizes them from its inspector fields when the command line
has no region flag. `MapCaptureBootstrap` builds a trimmed world: static container with a no-op
Ethereum API and no disk cache, offline feature flags forcing the abgen pipeline and abgen LODs,
LOD container, terrain, roads, skybox, quality, plus the capture's own systems from
`MapCaptureWorldFactory`. No avatar, no scene runtime, no UI, no networking beyond content
requests.

`MapCaptureSceneFeeder` replaces the client's radius-based scene loading: it asks the abgen
registry for the scene definitions of a rectangle of parcels in 60-pointer batches, creates one
entity per scene with `SceneLoadingState.CreateHighQualityLOD()` and an ISS descriptor promise,
and once the descriptor resolves attaches `SceneLODInfo` so the client's own LOD systems assemble
LOD_0 from the per-asset bundles. Roads go through the client's road plugin. Parcels the world
manifest marks empty count as ready immediately.

`MapCaptureCamera` drives the client's Cinemachine rig with an orthographic top-down virtual
camera at priority 1000, renders a rectangle of parcels into an HDR accumulator, and writes the
block as PNG or JPEG. `MapCaptureJob` walks the region block by block, loads chunks, waits for
every parcel to be ready or the timeout, renders, unloads, and writes `manifest.json`.

Vocabulary, since it trips people up:

- Block: one output image. In client-map mode always 40x40 parcels at 512 px.
- Chunk: how many parcels are loaded at once. In client-map mode a block is loaded in parts of
  `--map-capture-chunk` parcels (default 20, so four quarters per block); each part is rendered
  into its quadrant of the block image before the next part loads. This exists because loading a
  whole 40x40 block at once crashed the Windows editor with a GPU error.
- Region: which parcels to cover. In client-map mode it only selects chunks: any parcel inside a
  chunk renders that whole chunk.

Client-map grid, measured against the live client: chunk `i,j` covers X from `-152 + 40i` to
`-113 + 40i` and Y from `113 - 40j` to `152 - 40j`; j counts southward. Whole grid is
`-152,-167` to `167,152`. Verified against the client's `3,2` and `4,4` images.

## Live scene mode (experiment, 2026-10-02)

`--map-capture-scene x,y` runs the scene occupying that parcel the way the client does, JavaScript and
full-resolution assets, and renders it alone into `scene_x_y.{png,jpg}` plus a manifest. No ISS, no LOD,
no region. It exists to compare a live render against the LOD render of the same parcel; the editor
inspector has an `Editor Live Scene` toggle and parcel for it.

How it works (`MapCaptureLiveScene.cs`, `MapCaptureBootstrap.CreateLiveSceneLoaderAsync`): the bootstrap
additionally initializes the per-scene world plugins (`StaticContainer.ECSWorldPlugins`) and builds the
client's `SceneSharedContainer` with the same null comms, profiles and MVC the play mode integration test
suite uses (`IntegrationTestsSuite.CreateStaticContainer`), plus no-op restricted actions
(`MapCaptureNullObjects.cs`). The loader fetches the definition through the capture world's registry
loader, enqueues a readiness report in `SceneReadinessReportQueue` so the scene's GLTF gathering waits for
its models, calls `LoadSceneSystemLogic.FlowAsync` (hashed content, `scene.json` override, `main.crdt`,
facade creation) and starts `StartUpdateLoopAsync` on the thread pool. None of the client's scene life
cycle systems (`ResolveSceneStateByIncreasingRadiusSystem`, `LoadSceneSystem`,
`ControlSceneUpdateLoopSystem`, `UnloadSceneSystem`) are involved. The camera entity is set on
`ExposedCameraData.CameraEntityProxy` because scene systems read the camera through it.

Readiness is the report reaching 1 (all GLTFs loaded, 60 s internal cap), the scene failing, or
`--map-capture-timeout`. The image is the scene's footprint plus a one-parcel margin, squared and centred,
at `--map-capture-ppp` capped to the 8192 px render target. Not verified in the editor yet; the first run
will tell whether a scene can be created while the character camera plugin is not booted.

Known gaps: no neighbouring scenes or roads are loaded around the live scene (the feeder is idle in this
mode), the wallet API throws into the scene, and billboards face the default camera data.

## Worlds mode (2026-10-06)

`--map-capture-worlds all|<listFile>` captures Decentraland worlds (worlds-content-server realms such as
`name.dcl.eth`) as satellite tiles on the same grid as Genesis City, so the client can load them from
`{satelliteUrl}/worlds/{worldName}/{level}/{i},{j}.ktx2`. Not run yet; written without a build.

Grid, generalized from client-map mode (`MapCaptureArgs.TryParseGrid`, `TileOf`, `TileMin`, `TileName`): level L
splits the 320x320-parcel square whose north-west parcel is `-152,152` into 2^L x 2^L tiles, i eastward, j southward.
At `--map-capture-level 4` (the worlds default) tile `i,j` covers X `-152+20i..-133+20i`, Y `133-20j..152-20j`; level 3
is the client-map chunk grid (client-map mode still defaults to it and accepts `--map-capture-level` too). Tiles are
`--map-capture-tile-px` (default 512) JPEG q95 (`--map-capture-jpeg` changes the quality), rendered 25% larger and
downscaled like client-map chunks (32 px/parcel at level 4). `--map-capture-chunk` loads each tile in parts (default 20,
so one load per level-4 tile; must divide the tile); the 300x300 worlds therefore never have more than 400 parcels
resident.

One process captures every world (`MapCaptureWorldsJob`): boot once without Genesis City (no Genesis realm or terrain),
then per world:

1. `MapCaptureRealm.ConfigureWorldAsync`: GET `{WorldServer}/{name}/about`, world manifest from
   `{AssetBundleRegistry}/worlds/{name}/manifest` through the client's `WorldManifestProvider` (404 or a non-`dcl.eth`
   name gives `WorldManifest.Empty`), `RealmData.Reconfigure` exactly like `RealmController.SetRealmExclusiveAsync`, so
   the realm kind becomes World. The previous world's manifest is disposed. The world's `skybox.fixedHour` is not applied;
   `--map-capture-hour` holds for every world.
2. `MapCaptureSceneFeeder.RequestWorldDefinitions`: the client's `LoadFixedPointersSystem` logic. With a manifest, the
   occupied parcels are posted to `WorldEntitiesActive` (`{AssetBundleRegistry}/entities/active?world_name={name}`) in
   batches of 1000 (the client posts them all at once); without one, one `GetSceneDefinition` per `scenesUrn` against
   `WorldContentServer` (`LoadSceneDefinitionSystem` is now in the capture world for this). Both loaders apply the AB
   manifest fallback, so the ISS gate sees a manifest version. Any failed definition request fails the world.
3. Terrain: `Landscape.LoadTerrainAsync` unchanged. `MapCaptureRealmController.WaitForFixedScenePromisesAsync` now
   returns the feeder's resolved definitions, so the client's `GenerateFixedScenesTerrainAsync` builds the world terrain
   from the manifest's occupied parcels (or the union of scene parcels) and hides Genesis's. A single scene with
   `landscapeTerrain: false` gets no terrain, as in the client.
4. Extent: the terrain as generated, not just the parcels. `WorldTerrainGenerator` builds one box over the bounding box
   of the occupied parcels plus `borderPadding` (2) plus 10% of the mean side (`TerrainModel` with `addExtraPadding`),
   so two far-apart clusters get one terrain spanning both (`palkia.dcl.eth`: parcels -3,-4..101,101, terrain
   -16,-17..114,114). The extent is `TerrainModel.MinParcel..MaxParcel` widened to the cliff meshes' bounds
   (`ITerrain.Cliffs`, mesh bounds so culled cliffs count), source `terrain`. When the world has no terrain
   (`landscapeTerrain: false`, terrain hidden) or the model does not hold the parcels, it is the parcels' bounds, source
   `parcels`. `MapCaptureWorldsJob.TerrainExtent`.
5. Tiles: every level-L tile the extent intersects, clipped to the grid (X -152..167, Y -167..152), rendered with
   `MapCaptureJob.RenderBlockInPartsAsync` (the client-map loads-per-block path factored out) given the world's scene
   parcels: a part (`--map-capture-chunk`) holding none is rendered straight away with no load, wait or unload, so
   terrain-only tiles cost a few frames per part; parts with scenes load, wait and unload as before. Requests are
   answered from the resolved definitions; parcels no scene claims are empty and ready at once. Outside the world's
   parcels the image shows what the client would: world terrain, ocean, cliffs, trees. A load of only empty parcels
   skips the cache flush.
6. Cleanup: definitions cleared, scenes unloaded, `CacheCleaner` + `Resources.UnloadUnusedAssets` as per chunk.

Gated on `realmData.IsGenesis()` like the client: roads, `limitHeightByParcels`, and the IpfsPath base (`Content` vs
`WorldContentServer`). ISS LOD_0 works unchanged: descriptor and bundle URLs depend only on the scene id. The ISS
requirement holds: a world scene without a descriptor (SDK6 scenes, which the client only runs live) fails fast and is
listed in `failedParcels`.

Output: `<out>/worlds/<name>/<level>/<i>,<j>.jpg`, `<out>/worlds/<name>/manifest.json` (status
`inProgress|complete|skipped|failed`, scenes, parcels, `parcelsWithoutScene`, `extent` (`minX`/`minY`/`maxX`/`maxY`
inclusive parcels before clipping, and `source` `terrain|parcels`), `tileCount`, tiles with `pendingParcels`/`failedParcels`
and seconds, totals, `allScenesLoaded`, timings for realm/definitions/terrain/tiles), and `<out>/worlds/run-summary.json`
rewritten after every world. Resumable: a world whose manifest says `skipped`, or `complete` with an `extent`, is
skipped; an `inProgress` or `failed` world, or a `complete` one without `extent` (captured before the terrain-wide
extent, parcel-touching tiles only), is redone, keeping tiles its manifest already lists and adding the terrain-only
ones. Delete a world's `manifest.json` to redo it keeping nothing listed, or its folder to start it from scratch. A world with
any parcel outside the grid (one today, `neverlandranch.dcl.eth`) is `skipped` with a reason. A failure (realm,
definitions, terrain, timeout, exception) marks the world `failed` with the error and the run continues; the exit code
is 1 if any world failed or any tile has pending or failed parcels.

Index facts checked 2026-10-06 (`GET https://worlds-content-server.decentraland.org/index` with a browser user agent):
1671 worlds, 115 of them not `*.dcl.eth` (no registry manifest by construction, so the URN path), 4266 level-4 tiles
touched by world parcels in the grid (more now that the whole terrain extent is captured; not recounted), the largest worlds 90,000 parcels (bitfiend, excitedhamsters, ontherise) and 12,855 (worldtrack). The abgen
registry answers both `/worlds/{name}/manifest` and `/entities/active?world_name=` with Windows versions.

`scripts/map_worlds_ktx2.py` converts `<out>/worlds/*/<level>/*.jpg` into a mirrored KTX2 tree with the same toktx
arguments and bottom-row-first orientation as `map_pyramid_ktx2.py` (it imports them), only for worlds whose manifest is
`complete` unless `--include-incomplete`, resumable.

Fallback not needed so far: if switching realms in one process turns out unsafe, a PowerShell loop running the build
once per world with a one-line list file gives the same layout, since every world is resumable on its own.

## Decisions taken (do not relitigate)

- Parity is non-negotiable: reuse the client's rendering, LOD assembly, terrain, roads and skybox
  rather than a separate project.
- ISS descriptor required. A scene the registry knows but that has no descriptor on
  `abgen-cdn.decentraland.org/LOD/lods-unity/manifests/{id}_InitialSceneState.json` fails fast and
  is listed as failed in the manifest. No fallback to the legacy baked LOD_0 on
  `ab-cdn.decentraland.org/LOD/0/`. That fallback was offered and not taken; it would be a small
  change in `MapCaptureSceneFeeder.AttachLodInfo` if wanted.
- Debug logs use `UnityEngine.Debug.Log` with a `[JUANI]` prefix so they are easy to grep and
  easy to delete before the PR is finalized.
- Bloom is disabled by default (a global volume overriding intensity to 0) and the sun lens flare
  is disabled, because both produced a white blob on the top-down view. `--map-capture-keep-bloom`
  restores bloom.
- Scenes are lifted by `ParcelMathHelper.SCENE_CONTAINER_Y_OFFSET` (0.1) to stop z-fighting with
  the terrain. This is done in `InitialSceneStateLOD` and `InstantiateSceneLODInfoSystem`, so it
  also affects the client; review that before the PR merges.
- Flat ground is drawn at `FLAT_GROUND_HEIGHT` (-0.05) instead of Y=0, in `GenerateGroundJob` (the
  instances, used by the depth and shadow passes) and `MountainLit_VertexFunctions.hlsl` (the forward
  pass). The instanced ground and the GPU-instanced roads were both exactly at Y=0 and the ground's
  `Offset 1, 1` did not reliably lose, so a few arbitrary road parcels showed red ground. This is a
  client change too (the client's own baked map has the same red road parcels); review it with the
  scene lift. The old GameObject terrain already had a -0.1 root shift for the same reason. The shader
  side is a literal `-0.05`: a `static const float` in that include compiled without errors but the red
  came back, and the literal fixed it again. Cause not understood; keep the literal.
- `LODGroup.ForceLOD(0)` on every assembled scene, because the scene LODGroups' thresholds assume
  a perspective camera and cull everything under a wide orthographic view.
- Unity's asset bundle cache is redirected to `--map-capture-cache` with no size cap so a full-city
  run does not evict itself and does not fill the system drive.
- Never run builds or tests to validate changes; the owner runs them.

## Things that went wrong and how they were fixed

- First renders showed the camera at the prefab pose: the Cinemachine brain is in ManualUpdate
  mode, so `MapCaptureBrainUpdateSystem` calls `Brain.ManualUpdate()` each frame, blends are Cut,
  and the render waits for `Brain.IsLive`.
- Empty parcels never became ready: `WorldManifest.IsParcelKnownEmpty` was added.
- "Very expensive GPU usage" editor crash: chunked loading, above.
- Whole scenes vanishing (Airdrops Tower): LODGroup culling, fixed by `ForceLOD(0)`. Not an
  asset problem.
- Scenes missing because abgen had never converted them: 247 scenes were absent from the abgen
  registry; they have since been converted (all present, `windows=complete`).
- Compile errors hit and fixed along the way, in case they resurface after a merge:
  `ToParcel` renamed to `ToParcelJson` on dev; `LensSettings.OverrideModes` not visible from the
  MCP assembly (wrapped in `FreeCameraProjectionOverride`); `IDiskCache<T>.Null` has a private
  constructor (`Null.INSTANCE`); `[Query]` needs `using Arch.System`; `LensFlareComponentSRP`
  not referenced from `DCL.Plugins` (found by name via `GetComponent(string)`); the bloom volume
  needed a reference to the SRP core runtime assembly in `DCL.Plugins.asmdef`.
- Cloudflare returns 403 to Python's default user agent; the coverage script sets one.
- Red ground patches on a few road parcels (first Windows capture, e.g. `-26,7`, `-11,7`): depth
  fighting between the Y=0 instanced ground and the Y=0 instanced roads, fixed by the flat ground
  height above. Dead ends, do not retry: lifting the road GameObjects (roads are drawn by GPU
  instancing, and the pooled GameObjects do not show in the capture), a near-zero camera FOV to force
  instanced LOD 0 (no change), and ground rising through (occupancy keeps road parcels flat). Grass,
  missing baked road data and overlapping scenes were also ruled out.
- First full build run crashed after 21 blocks with `d3d11: failed to create 2D texture ... 8007000e`
  (out of memory). The capture has no `ReleaseMemorySystem`, so caches were never evicted.
  `MapCaptureJob.UnloadAsync` now calls `CacheCleaner.UnloadCache(false)` and
  `Resources.UnloadUnusedAssets()` after every chunk, and `CacheCleaner.UnloadCache` skips caches
  that were never registered (`?.` instead of `!`; the capture has no avatar, emote or profile caches).
- Builds without the `GPUIRuntimeSettingsOverwrite` component had no trees: GPUI's shaders and
  resources are in Addressables (`loadShadersFromAddressables`), which only the project's GPUI runtime
  settings enable. `MapCapture.unity` now carries the same component as `Main.unity`.
- Build log visibility: the production log matrix drops ENGINE, LANDSCAPE and `Debug.Log`, so the
  lines needed from a build run use `ReportHub.LogProductionInfo` with a `[MapCapture]` prefix (block
  written, descriptor failures per load, timeouts, abort, summary). Put `-logFile` before any `--` flag:
  the app's parser assigns every non-`--` token to the preceding flag.
- Still open: during play (not in the captured image) road corners flicker. Likely the pooled
  road GameObject and its instanced copy fighting at Y=0 with different meshes, since the road bake
  folds every `RoadTile*` variant into the `RoadTileL` mesh (`RoadSettingsAsset.HandleRoadTileCase`).
  Unconfirmed; check with the Frame Debugger if it ever shows in a capture.

## abgen coverage findings (2026-09-29)

- 24,377 scenes on the catalyst for the city. All are now in the abgen registry with the same
  entity id as the catalyst and Windows bundles complete.
- 207 scenes (one base parcel each in `abgen-no-descriptor.txt`) have no ISS descriptor. abgen's
  LOD lane log shows 244 failures, 240 of them "emitted no renderer state", 3 "zero placements",
  1 asset fetch. 37 of the failures are scenes that had an older descriptor, so it is a lane
  regression, not only new scenes. These render as empty terrain in the capture until abgen
  regenerates them; they are listed per block under `failedParcels` in the manifest.
- Of the failing scenes, 230 have a legacy baked LOD_0 on `ab-cdn.decentraland.org` (median 1.5
  MB, about 1 GB total). That is what the fallback above would use.
- The abgen owners have the list. When they report the lane fixed, rerun
  `scripts/abgen_coverage.py` and, for any chunk with failed parcels, rerun just that chunk.

## Next steps, in order

1. Editor sanity check on the Windows machine: one chunk, region `-32,-7` to `7,32` (chunk `3,3`,
   Genesis Plaza), real cache folder on the second disk, real output folder, timeout 600. Compare
   `3,3.jpg` to the client's `3%2C3.jpg`. Confirm the cache folder grows and the system drive does
   not. Check the manifest's `failedParcels` are only scenes from the no-descriptor list.
2. Build (`Editor.MapCaptureBuild.Build` in batch mode, or a regular build with `MapCapture.unity`
   as the only scene and addressables built first) and rerun the same chunk from the exe with
   `--map-capture-region -32,-7,7,32`. It should overwrite the image identically, download
   nothing, and exit 0.
3. Full run from the build without a region flag. Hours, dominated by download and assembly.
   Optionally split with two region flags across two runs.
4. Night pass: same with `--map-capture-hour 23` and another output dir; nothing to download.
5. Worlds at level 4: build, then run the worlds command in `docs/map-capture-handover.md` ("Worlds from a build"),
   first with a two-line list file (a small `dcl.eth` world with a manifest and a non-`dcl.eth` one) to check the realm
   switch, terrain and tiles, then `all`. Convert with `scripts/map_worlds_ktx2.py`.
6. Before the PR is opened: remove the `[JUANI]` logs, decide whether the 0.1 scene lift and the -0.05 flat ground stay in
   the client, review the `WorldManifest`, `InitialSceneStateLOD`, `InstantiateSceneLODInfoSystem`
   and `CinemachineExtensions` changes as client-facing, and do not commit editor noise (see the
   last section of `docs/map-capture-handover.md`).

## Open offers not yet taken

- Legacy LOD_0 fallback for descriptor-less scenes, recorded per parcel in the manifest.
- Day and night in one run.
- Per-chunk timing in the manifest.
