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
