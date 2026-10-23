# Phase A: rendering through Bevy while Unity is the host

Design refinements of [README.md § 8](README.md) as they were implemented. Each step's gate is a
local command or an Editor session; nothing here runs in CI (README § 1, non-goals).

## Step 2: first frame through Bevy (gate G-A1)

### What exists

| Piece | Where | Notes |
|---|---|---|
| ABI types | `Engine/crates/abi` | `#[repr(C)]` structs and enums only; `ABI_VERSION` is checked by `bm_init` and mirrored by hand in `BridgeAbi.VERSION` on the C# side. |
| Entry points | `Engine/crates/unity_bridge/src/lib.rs` | `bm_abi_version`, `bm_init`, `bm_shutdown`, `bm_begin_frame`, `bm_submit_snapshot`, `bm_resource_cmd`, `bm_get_output`, `bm_read_output`, `bm_render_event`, `bm_get_render_event_func`. Every one runs under `catch_unwind`; a panic is `BmResult::Panicked` plus a log line. The crate is the only one allowed `unsafe`. |
| Bevy app | `Engine/crates/unity_bridge/src/app.rs` | Headless plugin set (no winit, `WindowPlugin` without a primary window), one `App::update()` per submitted snapshot on the `bevy-migration` thread, then a device poll so `bm_begin_frame` only ever reports GPU-complete frames. |
| Output buffers | `Engine/crates/unity_bridge/src/output` | Two buffers. `Offscreen`: plain textures, pixels through `bm_read_output` (tests, the step 3 screenshot harness). `IoSurface` (macOS): one `IOSurface` per buffer wrapped as a Metal texture on Bevy's device (through wgpu-hal) and on Unity's device (through `IUnityGraphicsMetalV1`, captured in `UnityPluginLoad`). |
| Render plugin | `Engine/crates/render` | Handle table, flat entities with `GlobalTransform` written directly, camera and sun driven by the snapshot, placeholder cube and material, readback plugin. |
| C# bridge | `Explorer/Assets/DCL/BevyMigration/Bridge` | `BevyBridge` (lifecycle, external textures, log callback), `RendererRegistry`, `TransformGatherJob` (Burst, `IJobParallelForTransform`), `SnapshotWriter`, `BevyBridgeSystem` (global world, `PresentationSystemGroup`), `BevyBridgePlugin` (the composition-root hook), `BevyRenderedObject`. |
| Composite | `Explorer/Assets/DCL/BevyMigration/Composite` | `BevyCompositeFeature` blits the completed buffer after the skybox, before transparents and UI, on the game camera only. Installed on the URP renderer asset by `Decentraland/Bevy Migration/Install Composite Feature`. |
| Flag | `--world unity\|bevy`, debug panel "Bevy Migration" | `WorldRendererFlag` is the runtime truth; registered renderers get `forceRenderingOff` while Bevy owns them. |
| Bindings | `Explorer.Net/Bindings/NativeMethods.g.cs`, `Explorer/Assets/DCL/BevyMigration/Bridge/Generated/NativeMethods.g.cs` | csbindgen output written by `unity_bridge/build.rs`; both copies are generated files. |

### Conventions fixed here

- **Handedness.** Unity is left-handed, Bevy right-handed. The render crate mirrors Z: a renderer's
  local-to-world `L` becomes `S·L·S` and the camera transform is `S·V⁻¹` where `V` is Unity's
  `worldToCameraMatrix` (already OpenGL-style) and `S = diag(1,1,-1,1)`. Unit-tested in `render`.
- **Matrices** cross the ABI column-major, the shared layout of `Matrix4x4`, `float4x4` and `glam::Mat4`.
- **Output format** is BGRA8 sRGB on both sides (`Bgra8UnormSrgb`, `MTLPixelFormatBGRA8Unorm_sRGB`,
  `TextureFormat.BGRA32` with `linear: false`). wgpu writes rows top-down, so the composite blits with
  a vertical flip (`BevyComposite.FlipVertically`, toggleable from the debug panel).
- **Frame pacing.** Snapshot N is submitted and the composite reads the buffer of the latest completed
  frame, normally N-1. Bevy's thread waits for the GPU; Unity's threads never do.
- **Domain reloads.** `bm_init` is re-entrant (it shuts an existing bridge down first). `BevyBridge`
  shuts down on `beforeAssemblyReload`, on leaving Play mode and on quit.
- **Single buffer on the C# side.** The plan's "unmanaged double buffer" is unnecessary because
  `bm_submit_snapshot` copies everything out before returning; the gather job's `NativeArray` is reused.

### Running the gate

```bash
# Rust: unit tests plus the offscreen end-to-end test (renders a cube, reads pixels back)
cd BevyMigration/Engine && cargo fmt --check && cargo clippy --all-targets --locked -- -D warnings && cargo test
# Build and place the plugin
cargo build --release -p unity_bridge && cp target/release/libunity_bridge.dylib ../../Explorer/Assets/DCL/BevyMigration/Plugins/macOS/
# Unity, windowed Editor (batch mode cannot build URP's renderer, see docs/README.md § 7 step 1 notes):
cd ../.. && /Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity -projectPath "$PWD/Explorer" \
  -executeMethod DCL.BevyMigration.Editor.EditorPlaySession.RunBatch -bmAppParams "--world bevy --bevy-test-cube --mcp" -bmSeconds 90 -bmCycles 3
```

With `--mcp` on, `screenshot` with `worldOnly: true` through the MCP server shows the Bevy frame: the
blue-grey clear colour where Unity's world used to be and the orange placeholder cube in front of the
camera. The HUD stays on top. The three cycles enter Play mode, leave it, reload scripts and repeat;
each cycle logs "Bevy bridge initialized" once.

### Known limits at step 2

- Every registered renderer is drawn as the placeholder cube; meshes, textures and materials arrive in step 3.
- No depth is shared: Unity transparents composite over the Bevy frame without depth testing against it.
- macOS only. The Windows path (D3D11 shared handles) is step 5.
- Unity still renders its own opaques and skybox before the blit overwrites them; the camera culling mask stays untouched until step 5.

## Step 3: a full parcel (gate G-A3)

### What exists

| Piece | Where | Notes |
|---|---|---|
| Resource commands | `abi` (`BmResourceCmd`, `BmMeshHeader`, `BmTextureHeader`, `BmMaterial`), `unity_bridge` (`bm_resource_cmd`, `bm_upload_material`), `render/src/resources.rs` | Meshes and textures cross as byte payloads behind a header (`BmMeshHeader` + streams + indices + submeshes; `BmTextureHeader` + the full mip chain in Unity's own block format). Materials are typed (`bm_upload_material`) so csbindgen emits the struct. Handles are `u32`, allocated on the C# side, 0 reserved for the placeholders. Every decoder is bounds-checked; a malformed payload is logged and the handle lands in a rejected set. |
| Material family `UrpLit` | `render/src/materials/{mod.rs,urp_lit.wgsl}` | `ExtendedMaterial<StandardMaterial, UrpLitExtension>`: base colour, metallic and smoothness scalars, metallic-gloss map (the `DCL/Scene` glTF layout: roughness G, metallic B times the scalar, sent only when the material enables `_METALLICSPECGLOSSMAP`), normal map, occlusion map (G, blended by strength), emission, alpha modes (opaque, cutout, fade, transparent), double-sided, `SceneBounds` dithering outside the parcel rectangle. The Unlit family is the same material with `unlit` set. |
| Lights, fog, ambient | `SnapshotWriter`, `SceneContentScanner`, `render/src/lib.rs` | The sun (`RenderSettings.sun`, else the strongest directional light), the point and spot lights under the scene roots, `RenderSettings` fog, and the ambient probe averaged over six axes (works for flat, trilight and skybox ambient modes). |
| Scene content scan | `SceneContentScanner` | Every 60 frames: walks the `<parcel>_<name>_Container` roots, registers new `MeshRenderer`s with their uploaded mesh and materials, prunes destroyed ones. Skinned meshes, landscape, particles and skybox stay with Unity until their steps. |
| Screenshot harness | `BevyMigration/tools/parcel-screenshot.sh`, `Engine/tools/screenshot-diff` | A driven Editor session (`EditorPlaySession` + the MCP server) lands on a parcel, poses the free camera and saves a world-only PNG; the Rust tool compares two PNGs against a threshold. |

### Conventions fixed here

- **Winding.** Uploaded meshes keep Unity's local coordinates; the Z mirror of the world matrix flips
  the handedness, so every triangle's winding is reversed on upload (a,b,c to a,c,b) and front faces
  stay front faces.
- **Light units.** Unity's intensities are unitless multipliers of the albedo. Bevy's camera exposure is
  pinned to 1.0 (`UNITY_EXPOSURE`), a directional light of Unity intensity `I` becomes `pi * I` lux and a
  punctual light `4 pi^2 * I` lumens, so a white diffuse surface reflects the same value in both
  renderers. The ambient term is the probe average with brightness 1.
- **Tone mapping.** Bevy's camera uses `Tonemapping::None`: the composite lands before Unity's volume
  stack, which tone maps the whole frame once.
- **Textures.** Readable `Texture2D`s in a format wgpu accepts (BC1/3/4/5/7, RGBA8, BGRA8, R8, RG8)
  cross in their own block format with the full mip chain. Asset-bundle textures are GPU-only and
  `AsyncGPUReadback` rejects compressed sources outright, so they are blitted into an RGBA8 render
  texture (sRGB-encoded when the source is) whose generated mip chain is read back synchronously, once
  per texture.

### Running the gate

```bash
cd BevyMigration/Engine && cargo fmt --check && cargo clippy --all-targets -- -D warnings && cargo test
cargo build --release -p unity_bridge && cp target/release/libunity_bridge.dylib ../../Explorer/Assets/DCL/BevyMigration/Plugins/macOS/
cd ../.. && BevyMigration/tools/parcel-screenshot.sh unity /tmp/ga3/unity.png   # the reference
BevyMigration/tools/parcel-screenshot.sh bevy /tmp/ga3/bevy.png                  # the candidate
cd BevyMigration/Engine && cargo run -p screenshot-diff -- /tmp/ga3/unity.png /tmp/ga3/bevy.png --out /tmp/ga3/diff.png
```

Each harness run is a 4 minute Editor session landing on parcel 0,0 with the free camera at a fixed pose.

### Result on 2026-10-23 (Genesis Plaza, parcel 0,0, M1 Pro)

The parcel is recognisable through Bevy (geometry, textures, signs, props, point lights) but the
threshold is not met: mean absolute difference 58 (limit 4), 93% of pixels differ (limit 2%). The
differences are structural, not bugs in the transfer:

- Unity's skybox versus Bevy's flat clear colour (step 5 disables the skybox behind `world=bevy`; a
  matching sky is a later family).
- Unity's lighting beyond the snapshot: baked and reflection probes, the sun's shadow settings, bloom
  on emissives, and the volume stack's colour grading apply to the Unity frame and only partly to the
  Bevy frame.
- Avatars: Unity draws them before the composite overwrites the opaque base, so they are missing until
  step 4 moves them to Bevy.

The ledger rows for scene content (`DCL/SDKComponents`, `DCL/Rendering`) move to `in-progress`
rather than `bevy`; the gate stays open and is re-run with the step 5 screenshot suite.

### Known limits at step 3

- Normal maps read back through the RGBA8 blit lose the two-channel BC5 encoding Bevy expects; lit
  surfaces with normal maps look flat.
- The read-back copies are uncompressed RGBA8 (4 to 8 times the VRAM of BC7); acceptable in internal
  builds per README § 8, revisit with the step 5 budget.
- `SceneBounds` dithering is wired but no scene bounds are sent yet (`scene_bounds_enabled` is 0).
- Skinned renderers, world text, media surfaces and particles are not scene content yet (step 4).
- The sync readbacks run at scan time (every 60 frames) and hitch once per newly loaded scene.

## Step 4: avatars, text, media (gate G-A4)

### What exists

| Piece | Where | Notes |
|---|---|---|
| Skinned meshes | `abi` (`mesh_attributes::BONES`, `BmMeshHeader::bindpose_count`, `BmSkinned`, `BmSnapshot::{skinned, bones}`), `ResourceUploader.SkinnedMesh`, `render/src/resources.rs` | A wearable mesh is uploaded once per (mesh, spring-bone offset) with `[u16; 4]` joint indices, `[f32; 4]` weights and the inverse bind poses laid out for the avatar skeleton; the bridge mirrors them like the world matrices. |
| Skeletons | `RendererRegistry.RegisterSkeleton`, `AvatarContentScanner`, `SnapshotWriter` | One skeleton per avatar: the 62 base bones of `AvatarBase.AvatarSkinnedMeshRenderer.bones`, then every wearable's spring bones (indices offset per wearable, as the compute skinning does). Joint world matrices are gathered by the same Burst job as the renderers and travel in the snapshot. |
| Skinning | `render/src/lib.rs` (`sync_joints`, `SkinnedMesh`) | Bevy's own vertex-shader skinning: one joint entity per bone with its `GlobalTransform` written from the snapshot, `SkinnedMesh { inverse_bindposes, joints }` on the renderer entity (re-bound when its skeleton changes). The plan's WGSL compute port is not needed for the gate and was not written. |
| Avatar materials | `AvatarContentScanner` | The wearable's original asset-bundle material (`CachedAttachment.OriginalAsset.RendererInfos`) through the `UrpLit` family; skin and hair colours, the Toon family and outlines are not applied yet. |
| World text | `BmMaterialFamily::TmpSdf`, `urp_lit_flags::SDF_TEXT`, `urp_lit.wgsl` | TextMeshPro world text is already a `MeshRenderer` under the scene roots; its material maps to the SDF family: unlit, `_FaceColor` times the vertex colour, coverage from the atlas alpha, cutout at 0.5. |
| Media surfaces | `ResourceUploader.RefreshDynamicTextures`, `render/src/resources.rs` | A material whose base map is a `RenderTexture` (UUAV output) is uploaded through the blit readback and re-uploaded under the same handle on every scan; the bridge replaces the `Image` asset in place so bound materials pick it up. |

### Conventions fixed here

- **Mirroring, corrected.** Every Bevy frame is Unity's mirrored (`S * M * S` for renderers, camera and
  joints), so uploaded positions, normals, tangents and inverse bind poses have their Z negated as
  well. Both cameras keep X right and Y up, so the on-screen winding survives the mirror and the
  index reversal from step 2 stays. Step 2 did not mirror the vertices, which drew every mesh
  mirrored along its local Z; the symmetric placeholder cube and near-symmetric parcel props hid it.
- **Skeleton layout.** Joint `i < 62` is base bone `i`; a wearable's spring bone `k` is joint
  `62 + springOffset + k`; bind poses the mesh does not provide are identity.
- **A boned mesh is hidden until its skin binds.** Bevy chooses the skinned pipeline from the mesh's joint
  attributes but the bind group from the skin uniforms, so a boned mesh drawn without a bound skin is a wgpu
  validation panic that kills the render thread (every later upload then returns `Internal`). `sync_renderers`
  keeps such a renderer `Visibility::Hidden` until its joint set and bind poses resolve.

### Running the gate

```bash
BevyMigration/tools/parcel-screenshot.sh unity /tmp/ga4/unity.png --look-player
BevyMigration/tools/parcel-screenshot.sh bevy /tmp/ga4/bevy.png --look-player
cd BevyMigration/Engine && cargo run -p screenshot-diff -- /tmp/ga4/unity.png /tmp/ga4/bevy.png
```

### Result on 2026-10-23 (own avatar at idle on Genesis Plaza, M1 Pro)

The avatar renders through Bevy: skinned by Bevy from the snapshot's bone matrices, posed as Unity
poses it, with the wearables' textures, next to the remote avatars; world text (`Fishing Game`,
`Decentraland Updates`, the store signs) is readable and not mirrored. The pixel threshold stays
uninformative on a live plaza: two Unity captures of the same pose taken minutes apart already
differ in 83% of pixels (other avatars, animated props, time of day), so the threshold needs a
static test parcel, which is step 9 content work. For the record, the fixed-pose parcel capture
after the mirroring fix measures mean absolute difference 49 with 78% of pixels differing against
the step 3 Unity reference (58 and 93% before it).

### Known limits at step 4

- Avatar materials go through `UrpLit` with the asset-bundle textures: no Toon cel shading, no
  outlines, no skin or hair colour, no facial feature masks.
- Media surfaces refresh at the scan rate (every 60 frames) through a synchronous readback; the
  per-frame push through the resource channel is open.
- Blend shapes (facial expressions) are not in the snapshot.
- The emote-frame half of the gate was not run: it needs a deterministic emote frame on both sides.

## Step 5: Unity renders no world (gate G-A5, macOS)

### What exists

| Piece | Where | Notes |
|---|---|---|
| Camera takeover | `BevyBridgeSystem.TakeOverCamera` | Behind `world=bevy` the main camera's culling mask is emptied and its clear flags set to a solid colour, so Unity draws no world geometry and no skybox pass; only the composite feature and the UI remain. The previous mask and flags are restored when the flag flips back. |
| Deferred families | `ledger.csv` | Landscape, roads (GPU instancing) and both skyboxes move to `planned`, step 11; particle systems are SDK components and stay with that row. None of them is drawn behind `world=bevy`. |
| Gather budget | `BevyBridgeSystem.TrackBudget` | Gather job plus snapshot submit measured per frame (smoothed), shown in the debug widget and logged every 600 frames as `Bevy gather+submit ... ms/frame`. |
| Windows | deferred | See README § 2 "First platform": the D3D11 shared-handle path is out of scope until macOS is validated. |

### Result on 2026-10-23 (Genesis Plaza, M1 Pro, 2560x1440 Editor)

With `world=bevy` the main camera's mask is the parked virtual-camera layer only, so Unity draws no
world and no skybox; the world-only MCP screenshot shows only Bevy's frame under the UI, and the
free camera keeps working (Cinemachine needs its virtual cameras inside the mask, hence the parking).
Gather plus submit, smoothed per frame, from the Editor log:

| Registered | Bones | Gather + submit |
|---|---|---|
| 16 renderers | 74 | 0.08 ms |
| 2017 renderers | 74 | 3.05 ms (scan frame included) |
| 2768 renderers | 1286 | 1.95 ms |
| 3952 renderers | 1472 | 3.05 ms |

The gather itself is a Burst job; the cost scales with the registered count because the snapshot
is rebuilt each frame (material table, flags, skinned entries). Budget for the reference machine:
3 ms at the Genesis Plaza load, the scan frame (every 60 frames, resource uploads included) being
the spike to address when the readbacks go asynchronous.
