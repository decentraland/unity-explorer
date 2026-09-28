# Map capture handover: whole Genesis City from the Unity Editor on Windows

Goal: open the `MapCapture` scene in the Unity Editor, point Unity's asset bundle cache and the
image output at a chosen folder on a chosen disk, and render every satellite chunk of Genesis City.

Replace these two placeholders everywhere below before starting:

- `<CACHE_DIR>`: the asset bundle cache folder, e.g. `D:\dcl-map\bundle-cache`
- `<OUTPUT_DIR>`: the image output folder, e.g. `D:\dcl-map\satellite`

Both must be on the disk with free space. Budget tens of gigabytes for `<CACHE_DIR>`: it ends up
holding every scene asset of Genesis City at full quality.

## What the scene does

`Assets/Scenes/MapCapture.unity` boots only the rendering and loading parts of the client (asset
bundles, ISS LOD_0 assembly, roads, terrain, skybox, camera rig), fetches scenes chunk by chunk from
the abgen registry and CDN, renders each chunk straight down through an orthographic camera and
writes one image per chunk plus a `manifest.json`. Code lives in
`Assets/DCL/Infrastructure/Global/MapCapture/`. Play mode stops by itself when the run ends.

In client-map mode it renders the minimap's own satellite grid: 8x8 chunks of 40x40 parcels, each a
512 px JPEG named `i,j.jpg` (column, row from the north), covering parcels -152..167 on X and
-167..152 on Y. That is the set the client downloads today from the genesis.city repository.

## Setup

1. Check out branch `chore/map-capture-tool` of `decentraland/unity-explorer` and run `git lfs pull`.
2. Open the Unity project at the `Explorer` folder (not the repository root) with Unity 6000.5.9f1.
   The first import takes a while.
3. In Window > Asset Management > Addressables > Groups, keep Play Mode Script on
   "Use Asset Database" (the default). No addressables build is needed in the editor.
4. Open `Assets/Scenes/MapCapture.unity` and select the `MapCapture` object in the Hierarchy.
5. Fill the "Editor run" section of the inspector:

   | Field | Value |
   | --- | --- |
   | Editor Client Map | on |
   | Editor Region Min | -152, -167 |
   | Editor Region Max | 167, 152 |
   | Editor Output Dir | `<OUTPUT_DIR>` |
   | Editor Bundle Cache Dir | `<CACHE_DIR>` |
   | Editor Hour | 10 |
   | Editor Camera Height | 250 |
   | Editor Load Timeout Sec | 600 |

   Block size and pixels per parcel are ignored in client-map mode; the grid fixes them at 40
   parcels and 512 px. Save the scene so the values persist.

## Run

Press Play. The Console prints one line per chunk:

```
[MapCapture] block (x,y) written; pending N, failed M
```

Expect a few minutes per chunk, dominated by asset download and assembly, so the full grid of 64
chunks is a matter of hours. The Game view shows the top-down camera while it runs. Play mode
stops itself at the end and logs a summary of how many chunks rendered with every scene loaded.

Nothing else writes to the system drive: the run uses Unity's bundle cache redirected to
`<CACHE_DIR>` with no size cap, and the client's own disk cache is disabled in this scene.

## Verify

`<OUTPUT_DIR>` should hold 64 files `0,0.jpg` … `7,7.jpg` and `manifest.json`. In the manifest,
each block lists `pendingParcels` (never finished loading before the timeout) and `failedParcels`
(a scene whose LOD_0 failed to assemble). Both should be empty; report any that are not.

Alignment check: `3,1.jpg` must match the client's current
`https://media.githubusercontent.com/media/genesis-city/parcels/new-client-images/maps/lod-0/3/3%2C1.jpg`
feature for feature (same grid, lower detail on the client's side).

## Retry a chunk

Set the region to any parcel inside the chunk and press Play again; the file is overwritten in
place. Chunk `i,j` covers X from `-152 + 40*i` to `-113 + 40*i` and Y from `113 - 40*j` to
`152 - 40*j`. Everything already in `<CACHE_DIR>` is reused, so a retry only re-downloads what is
missing.

## Night pass

Same settings with Editor Hour 23 and a different output folder. With the cache populated by the
day pass it downloads nothing.

## If it fails

- A compile error: report it with the file and line; do not work around it.
- An exception at boot names the stage (static container, textures, LOD container, plugin
  initialization, realm, terrain). Report the full message.
- Chunks with many pending parcels: raise the load timeout, or run the region in halves
  (X -152..7 and X 8..167) if the editor is under memory pressure.
- Images that are sky or a horizon instead of a top-down view: the camera brain is not being
  updated; report it, this is a code problem.

## Do not commit editor noise

Unity creates files when the project opens that must not be committed: anything under
`Assets/GPUInstancerPro/Resources/`, changes to
`Assets/Rendering/UniversalRenderPipelineGlobalSettings.asset`, TMP font assets, and any
`Packages`, `ProjectSettings` or `UserSettings` folders that appear at the repository root (those
mean Unity was opened at the wrong level). The saved `MapCapture.unity` scene with the filled
inspector values is the only intended change from this handover.
