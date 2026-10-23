# Unity Explorer to Bevy: Migration Plan

Status: approved direction, pre step 0.
Audience: engineers and coding agents executing migration PRs in this repository.
Canonical location after step 0: `BevyMigration/docs/README.md` (this file moves there unchanged).

---

## 0. How to use this document

1. Read sections 1 to 6 fully before touching anything. They contain decisions that are closed and rules that CI enforces.
2. Find your step in section 7. Each step lists deliverables, files, its exit gate and the ledger rows it moves.
3. One PR implements one capability from one step. If your change does not map to a single step and a single gate, split it.
4. Every PR that touches `BevyMigration/**` or `Explorer/Assets/DCL/BevyMigration/**` must update `BevyMigration/ledger/ledger.csv` or state in the description why no row changes.
5. Never mark a ledger row `bevy`, `shim`, `web` or `deleted` without naming the gate that proves it.
6. The Unity client is the oracle. When the plan says "matches Unity", it means an automated comparison in CI, not a visual check by a person.
7. Do not relitigate section 2. If a decision there blocks you, open an issue labelled `migration-decision`; do not work around it in code.

---

## 1. Goal and non-goals

### Goal

Make the desktop Explorer client run on Bevy without the Unity runtime, with the existing C# application code running on a hosted CoreCLR behind a UnityEngine-compatible shim, and then port that C# to Rust feature by feature until the host is removed.

The first deliverable is the **host flip** (step 10): the desktop client boots, logs in, loads a parcel, renders scenes and avatars, and chats, with no Unity runtime in the package.

### Order of work

1. Rendering moves to Bevy while Unity remains the host (steps 2 to 5).
2. In parallel, the UnityEngine-compatible shim is built and verified on a Unity-free test bench (steps 6 to 7).
3. UI moves to a web renderer (step 8).
4. Content stops depending on AssetBundles (step 9).
5. Host flip (step 10).
6. C# is sunk into Rust feature by feature (step 11).

### Non-goals until after the host flip

- WebAssembly target. CoreCLR hosting does not run in a browser. Keep Rust-side choices wasm-aware, do not build for it.
- Production releases of hybrid builds. Everything before step 10 is internal.
- One-to-one visual parity for landscape, particle systems and the stylised skybox. These are disabled in hybrid builds and return in step 11.
- Replacing Unity as the authoring tool. The Unity Editor remains the content tool; its output is exported.

---

## 2. Decisions already made

| Decision | Choice | Rationale |
|---|---|---|
| Target engine | Bevy, current stable at step 0, pinned per workspace | Stated goal. Expect to carry a fork (see section 14). |
| Transitional C# runtime | CoreCLR hosted in-process through hostfxr | Fast iteration on existing logic. Real .NET replaces IL2CPP. |
| Facade shape | UnityEngine-compatible, semantics preserved | The tested C# application code must run unchanged at the application layer. The shim is scoped by an allowlist and verified against Unity recordings, never by reading Unity. |
| Host during rendering phase | Unity | Unity can still render the same frame, which gives a built-in A/B for every rendering milestone. |
| UI | Web renderer (CEF offscreen on desktop), React | Two Unity UI stacks and TextMeshPro are more expensive to shim than to rewrite. |
| Hybrid builds | Internal only | Removes release-quality, crash isolation and VRAM parity constraints until step 10. |
| First platform | macOS, Metal | Development machines. Windows follows inside the rendering phase through D3D11 shared handles. |
| Editor Play mode | Must keep working in hybrid builds | Developers iterate in the Editor through step 9. |
| Repository | This monorepo, two `BevyMigration` folders | Path-scoped CI already exists. The migration is self-contained and promotable at the flip. |
| Scene runtime during transition | ClearScript V8 on CoreCLR, unchanged | Runs on plain .NET. Moves to Rust in step 11 behind a flag. |
| Asset bundles | Unity loads them until step 9; replaced by exported files and source glTF before the flip | Bundles cannot be opened without Unity, so the flip depends on step 9. |
| Resource transfer in the render bridge | Copy once per asset first; zero-copy device sharing only if measurements require it | Backend-agnostic and fast to build. VRAM doubling is acceptable in internal builds. |
| Prefabs and controllers | Exported from the Editor at build time, never parsed from YAML at runtime | Simpler, faster, no serialization edge cases. |
| Transpilation | Not a strategy. A Roslyn-based migration compiler may assist step 11 for mechanical code only | No faithful C# to Rust translation exists. |
| Existing bevy-explorer | Reference and parts bin only, not a dependency | Different architecture (no C#). Its Bevy fork is the best evidence of what a streaming client needs from Bevy. |

---

## 3. Facts about the codebase

Measured on `Explorer/Assets`, excluding `Plugins`.

| Metric | Value |
|---|---|
| C# files / LOC | 5,219 / ~701k |
| Files referencing UnityEngine | 2,678 (51%) |
| Assemblies (asmdef) | 57 |
| Feature directories under `Assets/DCL` | 198 |
| ECS systems / source-generated `[Query]` methods | 317 / 635 |
| MonoBehaviours / ScriptableObjects / prefabs | 372 / 94 / 706 |
| Files with `[SerializeField]` | 622 |
| Lifecycle method files (Awake, Start, OnEnable, OnDisable, OnDestroy) | 242 |
| GetComponent call sites / Instantiate call sites | 337 / 158 |
| uGUI / UI Toolkit / TextMeshPro files | 321 / 181 / 249 |
| MVC controllers / views | 72 / 75 |
| UniTask files / PlayerLoopTiming references | 1,146 / 89 |
| Shader, compute and shadergraph files | 117 |
| Files on ComputeShader (GPU skinning etc.) | 37 |
| Animator files | 81 |
| Physics / Collider files | 51 / 59 |
| Burst / Jobs files | 32 / 43 |
| Test files (NUnit, NSubstitute) | 657 |
| Rust already in repo | UUAV (video), RustSegment (analytics), audio-analysis, rust-ethereum via UPM |
| Windows graphics API | D3D11, flip-model swapchain |
| Unity version | 6000.5.x, IL2CPP on standalone |

Dependencies that run on plain .NET without the shim: Arch, Arch.SystemGroups, UniTask (NuGet, with a custom frame pump), Unity.Mathematics (build from source, check licence), Newtonsoft, protobuf, rpc-csharp, Nethereum, ClearScript, Sentry .NET, DOTween (pure C# over shim types).

---

## 4. Repository structure

```
unity-explorer/
  Explorer/
    Assets/DCL/BevyMigration/
      BevyMigration.asmdef            runtime assembly; nothing else in Explorer references it
      Bridge/                         renderer registry, TransformAccessArray gather job, resource upload,
                                      snapshot writer, native entry points
      Composite/                      URP render feature that blits the Bevy target as the opaque base
      Recorder/                       lifecycle, hierarchy, coroutine and physics trace capture
      Inventory/                      runtime shader-family logger
      Plugins/                        compiled bridge library per platform, .meta carries import settings
      Editor/
        BevyMigration.Editor.asmdef
        Exporter/                     prefabs, ScriptableObjects, Animator controllers to export format
        Tools/                        menu items: capture recording, dump shader families, export all
  BevyMigration/
    Engine/                           Rust workspace
      Cargo.toml
      rust-toolchain.toml
      crates/abi/                     extern "C" surface, csbindgen input
      crates/unity_bridge/            cdylib Unity loads: snapshot ingest, handle tables, resource upload
      crates/render/                  Bevy render plugin: material families, skinning compute, offscreen target
      crates/shim_native/             object model storage, transforms, rapier, kira, input backing
      crates/content/                 export-format loaders, IPFS asset source, glTF URI resolver
      crates/host/                    CoreCLR hosting through hostfxr, tick, function tables
      crates/app/                     desktop binary
    Explorer.Net/                     .NET solution, no Unity
      Explorer.Net.sln
      Directory.Build.props
      UnityEngine.Shim/               UnityEngine-compatible assemblies
      Generated/                      one csproj per asmdef, generated, gitignored
      Bindings/                       csbindgen output, gitignored
      tools/asmdef2csproj/
      tools/api-inventory/
      tools/allowlist-analyzer/
    ledger/
      ledger.csv
      allowlist.toml
      shader-families.md
      recordings/
    docs/
      README.md                       this document
      <one file per phase as designs are refined>
```

Rules for the structure:

- Nothing outside `Explorer/Assets/DCL/BevyMigration` may reference the `BevyMigration` assemblies. Hooks are the URP renderer asset (composite feature) and one bootstrap plugin registered in the composition root behind a feature flag.
- The two new asmdefs fall under the existing lint ratchet and `.editorconfig`. Code there follows the repository code standards.
- CI: one new path-scoped workflow for `BevyMigration/**` (cargo fmt, clippy, test; dotnet build and test against the shim; ledger check). Existing Explorer workflows pick up the Unity-side folder automatically.
- The repo-root `BevyMigration/` is promoted at the host flip: `Engine/` becomes the product root, `Explorer.Net/` shrinks until deleted.

---

## 5. Working rules

### PR rules

- One capability per PR. Named in the title with the step: `feat(m3): URP Lit transparency modes`.
- Diff ceiling of about 800 changed lines excluding generated files. Generated files are listed in `.gitattributes` as `linguist-generated`.
- Every PR adds or updates a gate (a test, a recording replay, a screenshot case) or states which existing gate covers it.
- Every PR description has a paragraph "What the gate proves". If it cannot be written, the PR is too big.
- Ledger rows in the PR footer: `Ledger: ECS.Unity.Transforms -> bevy (G-A3)`.
- No PR mixes Unity-side and Rust-side changes unless the ABI changed; then the PR contains the ABI change, the C# side and the Rust side, and nothing else.

### Flags, not reverts

- Any capability that replaces an existing one ships behind a flag with the old path intact: `world=unity|bevy`, `scene-runtime=clr|rust`, `physics=unity|shim`.
- A regression is a flag flip. The old path is deleted only when the ledger row moves and the gate has been green for two weeks.

### Oracle

- Unity is the reference implementation. Every "matches" claim is a CI comparison: trace replay, byte equality, or screenshot diff within a threshold.
- Recordings live in `BevyMigration/ledger/recordings/` and are regenerated by an Editor menu item, never edited by hand.

### Agents

- Agents follow the same ceiling and gate rules. An agent PR that cannot name its gate is closed, not reviewed.
- Agents do not change `allowlist.toml` to make the analyzer pass. The allowlist shrinks by refactoring call sites inside Unity (step 1) and grows only by a human-approved PR that also adds the shim implementation and its conformance recording.
- Agents do not add `// ReSharper disable` comments or widen `scripts/lint/filter-warnings.sh`.

### Commits

- Conventional prefix plus step label: `feat(m6): coroutine scheduler on frame pump`.
- Nothing is committed on behalf of the user; agents leave changes uncommitted for review unless told otherwise.

---

## 6. Ledger and allowlist

### `BevyMigration/ledger/ledger.csv`

One row per asmdef (57) and per feature directory under `Assets/DCL` (198), plus rows added for shim subsystems and material families.

```
id,kind,target,status,step,gate,notes,prs
ECS.Unity.Transforms,asmdef,bevy,unity,3,G-A3,,
DCL.Chat,feature,web,unity,8,G-C1,,
UnityEngine.Physics,shim,shim,planned,7,G-B4,rapier,
Material.URPLit,family,bevy,unity,3,G-A3,,
```

- `kind`: `asmdef`, `feature`, `shim`, `family`.
- `target`: where the row ends up: `bevy`, `shim`, `web`, `deleted`, `keep` (stays C# until step 11).
- `status`: `unity`, `planned`, `in-progress`, `bevy`, `shim`, `web`, `deleted`.
- `gate`: gate id from section 7. Required for any status other than `unity`, `planned`, `in-progress`.
- CI renders counts per status into `BevyMigration/docs/README.md` under a "Progress" heading.

### `BevyMigration/ledger/allowlist.toml`

Generated once by `tools/api-inventory`, then maintained by hand.

```toml
[object_model]
members = [
  "UnityEngine.GameObject..ctor(string)",
  "UnityEngine.GameObject.GetComponent<T>()",
  "UnityEngine.Object.Destroy(UnityEngine.Object)",
]
phase = "B"

[rendering]
members = ["UnityEngine.Renderer.enabled", "UnityEngine.Material.SetFloat(int,float)"]
phase = "A"

[ui]
members = ["UnityEngine.UI.Button.onClick"]
phase = "C"
removed_by_refactor = ["UnityEngine.Animator.GetCurrentAnimatorStateInfo(int)"]
```

- `tools/allowlist-analyzer` is a Roslyn analyzer referenced by every generated csproj. Any use of a `UnityEngine`, `Unity.*` or `TMPro` member not in the allowlist is a build error.
- Groups have a `phase`. The shim must implement every member in groups with phase `B` before step 10.
- `removed_by_refactor` records members eliminated inside Unity so they are not re-introduced.

---

## 7. Steps

Gate ids: `G-<phase><n>`. Phases: A rendering, B shim, C UI, D content, E flip, F sink. Sizes are estimates for two to three engineers and will move; the gate is the exit criterion.

### Step 0: Scaffold

Purpose: a place for every piece, traceability from the first PR.

Deliverables:
- `BevyMigration/Engine` workspace with empty crates listed in section 4, pinned toolchain, `cargo fmt`, `clippy -D warnings`, `cargo test` in CI.
- `BevyMigration/Explorer.Net` solution with `UnityEngine.Shim` (empty), tool skeletons, `Directory.Build.props` (nullable on, warnings as errors, LangVersion latest).
- `Explorer/Assets/DCL/BevyMigration` with both asmdefs and empty folders.
- `ledger.csv` seeded with all asmdefs and feature directories at status `unity`.
- `.github/workflows/bevy-migration.yml` path-scoped to `BevyMigration/**`, plus `ledger-check.yml` that fails when migration paths change without a ledger change or an explicit `Ledger: none` footer.
- PR template with step label, gate, "What the gate proves", ledger footer.
- This document moved to `BevyMigration/docs/README.md`.

Gate `G-01`: CI green on an empty workspace; ledger check fails a test PR that omits the footer.

### Step 1: Inventories

Purpose: exact scope of the shim and of the material families.

Deliverables:
- `tools/api-inventory`: loads all asmdefs with Roslyn, emits every `UnityEngine`, `Unity.*`, `TMPro` member used with file and count to `ledger/api-inventory.json`, and generates `allowlist.toml` grouped by subsystem.
- `Explorer/Assets/DCL/BevyMigration/Inventory`: runtime logger recording every shader bound by any renderer in a play session, with material counts, to `ledger/shader-families.md`. Triggered by an Editor menu item and by a command-line flag in player builds.
- Allowlist groups assigned a phase.
- First refactor PRs inside Unity: wrap Animator state queries, Cinemachine access, Addressables load-state polling and any member the inventory shows in fewer than three files behind project facades. Each refactor moves members to `removed_by_refactor`.

Gate `G-11`: allowlist generated, every group has a phase, shader families documented with per-family renderer counts, inventory tool runs in CI and fails if a new member appears outside the allowlist.

### Step 2: First frame through Bevy

Purpose: Bevy renders inside the Unity window, under the real HUD, on macOS, in Editor Play mode.

Deliverables:
- `crates/abi`: `extern "C"` functions `bm_init`, `bm_shutdown`, `bm_begin_frame`, `bm_submit_snapshot`, `bm_render_event` (Unity render-thread callback), `bm_resource_cmd`. Every entry wrapped in `catch_unwind`; panics become error codes and a log line. `csbindgen` generates the C# `DllImport` side into `Explorer.Net/Bindings` and a copy into `Explorer/Assets/DCL/BevyMigration/Bridge/Generated`.
- `crates/unity_bridge`: cdylib, owns a Bevy `App` on its own thread, headless (`RenderPlugin` without a window), camera with image render target, double-buffered output exported as IOSurface on macOS.
- `crates/render`: `BevyMigrationRenderPlugin` with handle tables and a flat entity set (`GlobalTransform` written directly, no `Transform`, no hierarchy).
- `Explorer/Assets/DCL/BevyMigration/Bridge`: `RendererRegistry` (register, unregister, handle allocation), `TransformGatherJob` (`IJobParallelForTransform` over a `TransformAccessArray`), `SnapshotWriter` (SoA into unmanaged double buffer), `BridgeBootstrap` (feature flag, init/teardown, domain reload safe).
- `Explorer/Assets/DCL/BevyMigration/Composite`: URP `ScriptableRendererFeature` that imports the shared texture and blits it as the opaque base before transparents and UI.
- Flag `world=unity|bevy` (app argument and debug widget).

Gate `G-A1`: one cube with URP Lit shows through Bevy with the HUD on top; flag flips at runtime; Play mode survives three consecutive domain reloads.

### Step 3: A full parcel

Purpose: all SDK scene content drawn by Bevy.

Deliverables:
- Resource commands: mesh (all vertex streams, indices, submeshes, bounds), texture (raw block-compressed data, mips, sRGB, sampler), material (family id plus property blob), with `AsyncGPUReadback` for non-readable assets. Generational `u32` handles allocated in C#.
- Material family `URPLit`: base colour, metallic, smoothness, normal, occlusion, emission, alpha modes (opaque, cutout, fade, transparent), double-sided, with `SceneBounds` extension for dithering outside parcel bounds.
- Lights, fog and active Volume values in the snapshot.
- Screenshot harness: `BevyMigration/Engine/tools/screenshot-diff` and a Unity batch-mode scene list; small resolution; threshold per case; runs on a self-hosted macOS runner or software Vulkan on Linux for the Bevy side.

Gate `G-A3`: fixed parcel set renders both ways within threshold; ledger rows for scene content rendering move to `bevy`.

### Step 4: Avatars, text, media

Purpose: avatars, nametags and video surfaces drawn by Bevy.

Deliverables:
- Skeleton upload once per skinned mesh; bone world matrices and blend shape weights per frame in the snapshot.
- Compute skinning ported to WGSL in `crates/render/skinning`.
- Material family `Toon`: cel shading, outlines, mesh tags for outline colours.
- Material family `TmpSdf` for TextMeshPro world text.
- Media textures: UUAV output frames pushed as texture updates through the resource channel.

Gate `G-A4`: fixed profile at idle and at a fixed emote frame matches within threshold; nametags and a video surface render.

### Step 5: Unity renders no world

Purpose: close the rendering phase on both platforms.

Deliverables:
- Unity main camera culling mask empty; only composite feature and UI remain.
- Windows path: D3D11 shared NT handles opened by a D3D12 wgpu device. Document the Unity player graphics API requirement.
- Landscape, particle systems and skybox disabled behind `world=bevy`, recorded in the ledger as deferred to step 11.
- Per-frame gather cost budget measured and documented.

Gate `G-A5`: screenshot suite green on macOS and Windows with `world=bevy` default; frame gather under budget on the reference machine.

### Step 6: Shim foundation (parallel with steps 2 to 5)

Purpose: the C# code compiles and its tests pass with no Unity in the room.

Deliverables:
- `tools/asmdef2csproj`: generates one SDK-style csproj per asmdef into `Explorer.Net/Generated`, honouring asmdef references, defines, `InternalsVisibleTo`, and excluding `Editor` folders. Unity test assemblies become NUnit projects.
- `UnityEngine.Shim` assemblies: `UnityEngine` (object model, `GameObject`, `Transform`, `Component`, `MonoBehaviour` lifecycle, `Object` fake-null semantics, coroutines, `Time`, `Application`, `Screen`, `SystemInfo`, `PlayerPrefs`, `Debug` routed to ReportHub, `Random`, `Mathf`, math structs), `Unity.Mathematics` from source, `Unity.Collections` subset over `NativeMemory`, UniTask frame pump that mirrors `PlayerLoopTiming` ordering.
- Lifecycle order implemented from recordings, not from documentation: Awake, OnEnable, Start, Update, LateUpdate, OnDisable, OnDestroy; `Instantiate` ordering; `Destroy` deferral to end of frame; `DontDestroyOnLoad`.
- `Explorer/Assets/DCL/BevyMigration/Recorder`: records lifecycle events, hierarchy mutations, coroutine yields and timings for scripted scenarios into `ledger/recordings/<scenario>.jsonl`.
- `Explorer.Net/tools/replay`: runs the same scenarios under the shim and diffs traces.
- `tools/allowlist-analyzer` wired into every generated csproj.

Gate `G-B1`: `dotnet test` green for all Unity-free test assemblies; count recorded in ledger; three scenario recordings replay byte-equal.

### Step 7: Shim subsystems

Purpose: physics, audio, input and asset loading work without Unity, headless.

Deliverables:
- `crates/shim_native/physics`: rapier behind `Physics.Raycast`, `Physics.SphereCast`, `Physics.OverlapSphere`, `Collider` components, layers as collision groups, `Rigidbody` subset the inventory shows.
- `crates/shim_native/audio`: kira behind `AudioSource`, `AudioClip`, `AudioListener`, mixer groups the inventory shows.
- `crates/shim_native/input`: Bevy input behind the Input System actions and action maps the code uses, including rebinding.
- `crates/content`: Addressables members backed by a Bevy asset source over exported files (step 9 format) and source glTF.
- Physics query recordings captured in Unity and replayed.

Gate `G-B4`: physics, audio and input recordings replay within tolerance; Addressables calls in the startup path resolve against the content crate.

### Step 8: UI to web

Purpose: the UI no longer needs Unity.

Deliverables:
- `crates/render/cef` or a sibling crate: CEF offscreen rendering into a texture composited over the world; input routing by pixel opacity; text focus forwarding; cursor relay. Reference lineage: bevy_cef and bevy-explorer's `cef_offscreen`.
- System bus owned by Rust: typed envelopes, request and stream shapes, DTOs generated for TypeScript (ts-rs) and C# (serde to C# generator). C# never talks to the page directly.
- React app under `BevyMigration/Hud` (added to the structure at this step) with design tokens from the Explorer 2.0 design system.
- Controllers migrate one per PR; each flips one of the 72 controller rows to `web`. The Unity UI shrinks screen by screen.
- Playwright suite in mock-bridge mode; native smoke test: boot, login, walk, chat.

Gate `G-C1`: Unity renders no UI; Playwright and smoke green.

### Step 9: Content without AssetBundles

Purpose: every asset on the startup path loads from exported files or source glTF.

Deliverables:
- `Explorer/Assets/DCL/BevyMigration/Editor/Exporter`: exports prefabs (hierarchy, serialized fields by reflection, component data for allowlisted components), ScriptableObjects, Animator controllers (states, transitions, conditions, blend trees, masks, layers), and an addressables manifest, to the format in section 11. Runs at build time and from a menu item.
- `crates/content`: loaders for the export format; IPFS asset source with URI resolver; glTF loading of wearables, emotes and scenes from the content server.
- Startup trace tool: logs every bundle and addressable load; CI asserts zero bundle loads.

Gate `G-D1`: startup trace shows zero bundle loads in the shim test bench; a wearable set, an emote and a parcel load from source glTF.

### Step 10: Host flip

Purpose: the desktop client runs without Unity. First stated goal.

Deliverables:
- `crates/app`: Bevy owns window, input, frame; composes render, cef, content, host, shim_native.
- `crates/host`: loads CoreCLR through hostfxr (`netcorehost`), initialises the shim with the function table from `crates/abi`, calls `Tick(dt)` once per frame from an exclusive system; C# command buffers applied after the call.
- Render bridge snapshot path replaced by direct handle writes from the shim; `RendererRegistry` and `TransformGatherJob` deleted from the Unity side.
- `UnityEngine.Animator` facade backed by a Rust animation graph fed from exported controllers; bone matrices diffed per frame against recordings for locomotion and emotes.
- Packaging: self-contained .NET runtime, CEF framework and helper, no Unity player, no bundles, no bridge plugin.
- Sentry: native and managed crash reporting both wired.

Gate `G-E1`: boot, guest login, walk a Genesis City parcel, chat, ten-minute soak on macOS and Windows; package contains no Unity runtime; FPS floor on the reference machine.

### Step 11: Sink C#

Purpose: the full Rust client.

Order of porting, each item one or more PRs with the old path behind a flag:
1. Scene runtime: deno_core or rusty_v8 behind `scene-runtime=rust`; C# runtime stays as oracle until per-tick CRDT streams are byte-equal on all SDK7 test scenes.
2. Jobs and Burst paths, movement codec (byte equality), CRDT.
3. ECS features in ledger order, lowest dependency count first; the migration compiler (Roslyn to Rust skeletons) may be used for components, enums, DTOs and `[Query]` signatures only.
4. Services: profiles, social, chat pipeline, notifications, feature flags, analytics, settings, web requests.
5. Landscape, particles, skybox return as Rust.
6. Shim members deleted as their last caller goes; `allowlist.toml` shrinks to empty.
7. Host removed when the hosted assembly count reaches the written threshold.

Gate `G-F1` per item; final gate `G-F9`: zero hosted assemblies, no .NET runtime in the package.

---

## 8. Render bridge design (steps 2 to 5)

### Topology

- Bevy runs in-process as a cdylib loaded by Unity. Not a second process: data volume makes IPC the wrong tool.
- Unity owns window, swapchain, input, physics, animation, assets, UI.
- Bevy runs headless on its own thread: camera with image render target, double-buffered, one frame of latency. Each Unity frame: C# finalises snapshot N and signals; Bevy renders N while Unity composites N-1.
- Unity's main camera renders no world geometry. A URP render feature blits Bevy's target as the opaque base; Unity UI draws on top.

### Resource transfer

Phase 1: copy once per asset.
- Meshes: vertex streams (position, normal, tangent, uv0, uv1, colour, bone weights, bone indices), index buffer, submesh ranges, bounds, blend shape deltas. Non-readable meshes via `Mesh.GetVertexBuffer` and `AsyncGPUReadback`.
- Textures: raw block-compressed data in Unity's format (BC7, BC5, DXT), mip chain, sRGB flag, sampler. Non-readable via `AsyncGPUReadback`.
- Materials: family id plus property blob (floats, vectors, texture handles, keywords).
- Skeletons: bind poses, bone to vertex mapping.
- VRAM is doubled while Unity holds its copy. Accepted in internal builds.

Phase 2, only if measured necessary: shared device, zero copy. Create Bevy's render device from Unity's device through wgpu-hal (`device_from_raw` for Vulkan and Metal; verify availability for the pinned wgpu), wrap Unity's native textures and buffers directly. Metal on macOS is the easier target. Windows requires the player on D3D12 or Vulkan.

### Output sharing

- macOS: IOSurface-backed textures, both devices wrap the same surface.
- Windows: D3D11 shared NT handle created by Unity, opened by the D3D12 wgpu device.
- Synchronisation: double buffering and a per-frame fence or shared event; CPU wait before composite as fallback. No `waitUntilCompleted` on the render thread.

### Snapshot schema (SoA, unmanaged, double-buffered)

```
SnapshotHeader { frame: u64, renderer_count: u32, skinned_count: u32, light_count: u32, camera: CameraData, env: Environment }
Renderers      { handle: [u32], world: [Mat4], mesh: [u32], material_first: [u32], material_count: [u8], flags: [u16], bounds: [Aabb] }
Overrides      { renderer_index: [u32], prop_id: [u32], value: [Vec4] }           per-instance property block values
Skinned        { renderer_index: [u32], bone_offset: [u32], bone_count: [u16], blend_offset: [u32], blend_count: [u16] }
Bones          [Mat4]
BlendWeights   [f32]
Lights         { kind: [u8], world: [Mat4], colour: [Vec4], range: [f32], spot: [Vec2], shadows: [u8] }
Instanced      { mesh: [u32], material: [u32], matrix_buffer: [u32], count: [u32] }  landscape batches, step 11
CameraData     { view: Mat4, proj: Mat4, viewport: Vec4, near: f32, far: f32, post: PostValues }
Environment    { fog: FogValues, ambient: Vec4, reflection: u32 }
```

- Gathered by a Burst `IJobParallelForTransform` over a `TransformAccessArray`. Never a managed loop over `localToWorldMatrix`.
- Everything registered is sent; Bevy culls.
- Bevy side: handle tables map to entities with mesh, material and `GlobalTransform` written directly. No `Transform`, no hierarchy.

### Material families

From the step 1 shader inventory. Expected minimum: `URPLit` (all alpha modes, double-sided, emissive, scene-bounds dithering), `Toon` (avatars, outlines, mesh tags), `Unlit`, `TmpSdf`, `Video`. Each family is one `MaterialExtension` in `crates/render/materials/<family>.rs` with its WGSL beside it. Landscape, grass, water, particles and skybox families are deferred.

### Not data

- Shuriken particle systems: `ParticleSystemRenderer.BakeMesh` per frame as a transitional path if any are kept; VFX Graph is deferred.
- World-space TextMeshPro: ordinary meshes plus `TmpSdf`.
- Decals, water, grass: shader families, deferred.

### Editor

- Unity never unloads native plugins in the Editor. `bm_init` and `bm_shutdown` are re-entrant; all state is owned by the bridge and reset on init; the C# side re-registers everything after a domain reload.

---

## 9. Shim design (steps 6, 7, 10)

### Principles

- The shim implements the allowlist, nothing else. New members enter by a human-approved PR with implementation and recording.
- Semantics come from recordings captured in Unity, never from reading Unity or documentation alone.
- Where a member is expensive to implement faithfully, the call site is refactored inside Unity first (step 1 facades), where the real engine still validates it.

### Conformance recordings

- `Recorder` instruments: lifecycle callbacks with frame and order index, hierarchy mutations (parent, sibling index, active state), `Instantiate` and `Destroy` with deferral frame, coroutine yields with timing kind, physics queries with inputs and results, Animator bone matrices per frame for named clips.
- Scenarios are scripted scenes under `Explorer/Assets/DCL/BevyMigration/Recorder/Scenarios` and run in batch mode.
- `tools/replay` executes the same scenario code under the shim and diffs. Tolerances are per channel (exact for order and hierarchy, epsilon for physics and bones).

### Subsystem backing

| Unity surface | Shim backing | Step |
|---|---|---|
| `GameObject`, `Transform`, `Component`, lifecycle, coroutines | `shim_native/object_model`: arena of objects with generational ids, flat transform storage with lazy world-matrix cache, lifecycle scheduler on the host tick | 6 |
| `Object` null semantics | Shim `Object` overloads `==` to treat destroyed handles as null, as Unity does | 6 |
| `Time`, `Application`, `Screen`, `SystemInfo`, `PlayerPrefs` | Host-provided values; prefs in a JSON file beside config | 6 |
| `Debug` | ReportHub | 6 |
| UniTask `PlayerLoopTiming` | Frame pump driven from `Tick`, phases in Unity order | 6 |
| `Unity.Mathematics` | Source build | 6 |
| `Unity.Collections`, `Unity.Jobs` | `NativeArray` over `NativeMemory`; jobs run synchronously or on a thread pool; Burst is absent | 6 |
| `Renderer`, `Mesh`, `Material`, `Texture2D`, `Shader`, `Camera`, `Light` | Handles into `crates/render` (same tables as the bridge) | 10 |
| `Physics`, `Collider`, `Rigidbody` | rapier | 7 |
| `AudioSource`, `AudioClip`, `AudioListener` | kira | 7 |
| Input System | Bevy input with action maps | 7 |
| Addressables | `crates/content` asset source | 7, 9 |
| `Animator`, `AnimationClip`, Playables subset | Rust animation graph from exported controllers | 10 |
| Cinemachine, Animation Rigging | Refactored behind project facades in step 1; implemented on the Rust side as part of camera and IK work | 10 |
| uGUI, UI Toolkit, TextMeshPro UI | Not shimmed; removed by step 8 | 8 |

### Threading

- C# runs only inside `Tick`, called from one exclusive Bevy system. UniTask continuations run on that thread.
- C# writes through command buffers applied by Rust after `Tick` returns. C# never holds Rust references across frames except generational handles.

---

## 10. Web HUD design (step 8)

- CEF offscreen rendering into a texture composited over the world; same page on all desktop platforms.
- System bus owned by Rust in `crates/system_bus`: request and stream envelopes, typed DTOs in a Bevy-free crate exported with ts-rs for TypeScript and with a serde to C# generator for the shim side.
- C# features publish and subscribe on the bus through the shim; they never address the page.
- Input routing: pointer over opaque HUD pixel goes to the page, otherwise to the world; text focus forwarded; pointer lock honoured.
- UI scale mirrors the Unity `CanvasScaler` reference resolution.
- SDK scene UI (UiTransform, UiText, UiBackground, UiInput, UiDropdown) is engine-side and is not part of the HUD; it moves to Bevy UI in step 11.

---

## 11. Content and export format (step 9)

- Format: a documented binary container with a JSON index (`schema_version`, `assets[]`), one file per exported prefab, ScriptableObject, Animator controller and manifest. Serialization through a small shared Rust and C# schema crate; no Unity YAML at runtime.
- Prefab export: hierarchy, transforms, active state, allowlisted component data by reflection over `[SerializeField]` and public fields, GUID references resolved to asset ids.
- Animator export: layers, states, motions (clip ids and blend trees), transitions with conditions, parameters, masks.
- Manifest: addressable key to asset id and location.
- Content server: wearables, emotes and scenes loaded as source glTF with Draco and KTX2 where present; IPFS URIs resolved by the content crate's asset source. The asset-bundle converter gains a glTF and KTX2 output later as an optimisation, not as a dependency of the flip.

---

## 12. Host flip design (step 10)

- `crates/app` owns window, input, frame, and plugin composition.
- `crates/host`: `netcorehost` loads hostfxr from the bundled self-contained runtime; `UnmanagedCallersOnly` entry points for `Init`, `Tick`, `Shutdown`; a function table of Rust callbacks handed to C# at init; `csbindgen` generated signatures on both sides.
- Error handling: Rust panics never cross into .NET (`catch_unwind` at every entry); .NET exceptions never cross into Rust (caught at every `UnmanagedCallersOnly` boundary and reported through ReportHub and Sentry).
- Packaging: self-contained .NET runtime, CEF, assets manifest and exported files. No Unity player, no bundles, no bridge plugin.
- Kill criterion for the host, written at this step: hosted assembly count threshold and date, recorded in `BevyMigration/docs/host-kill-criterion.md`.

---

## 13. Expected Bevy fork

Evidence from the existing bevy-explorer fork (99 commits over 0.16.1) indicates the areas where a streaming open-world client patches Bevy. Expect to need equivalents and plan to upstream:

- Hitch avoidance: asynchronous pipeline compilation with notification, fallback materials, GPU transfer prioritisation and reuse, immediate upload paths.
- Streaming: cancelling in-flight asset loads on handle drop, pluggable glTF URI resolver, texture decode skipping, image downsizing.
- Render performance: render-world instance mirroring, previous-frame visibility tracking, dense animation evaluator storage, skin buffer growth, morph target limits.
- Features: light cookies, clustered decals, depth of field options, transparent sort by distance.

Own the fork as a workstream: one engineer, pinned alongside wgpu and winit, rebased per Bevy release, every patch tracked against an upstream PR.

---

## 14. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Shim grows into an engine | Allowlist enforced by analyzer; refactor call sites inside Unity first; new members only with recordings. |
| Shader parity takes longer than everything else | Step 1 inventory limits families; landscape, particles, skybox deferred; golden diffs per family. |
| Animator semantics | Recorded bone matrices per clip as the gate; port locomotion and emotes to an animation graph rather than emulating Mecanim generally. |
| VRAM doubling in hybrids | Accepted in internal builds; zero-copy device sharing as a measured escalation. |
| Per-frame gather cost | Burst job from the first milestone; budget measured at step 5. |
| Editor domain reloads | Re-entrant init and teardown required by `G-A1`. |
| Platform plumbing (IOSurface, DXGI handles, sync) | macOS first; Windows inside step 5; no `waitUntilCompleted`. |
| UniTask ordering across 1,146 files | Frame pump mirrors `PlayerLoopTiming`; coroutine and UniTask timings are part of recordings. |
| Bevy breaking releases | Pinned workspace; fork workstream; upgrade PRs isolated. |
| Content server glTF slower than bundles | Internal builds accept it; converter glTF and KTX2 output after the flip. |
| AI-generated oversized PRs | Diff ceiling, gate paragraph, ledger footer enforced by CI and review. |

---

## 15. Deferred list

Recorded in the ledger as `planned` with step 11:
- Landscape and GPU instancing, grass, water, decals.
- Particle systems (Shuriken and VFX Graph).
- Stylised skybox and look presets.
- LOD and imposters; asset-bundle converter glTF and KTX2 output.
- Quality settings beyond the minimum the HUD needs.
- WebAssembly target.
- SDK scene UI in Bevy UI.

---

## 16. Glossary

- Allowlist: the set of Unity members the shim must provide, enforced by a Roslyn analyzer.
- Family: a material family, one `MaterialExtension` plus WGSL in `crates/render`.
- Gate: an automated exit criterion with an id `G-<phase><n>`.
- Handle: a generational `u32` identifying a bridge or shim resource.
- Ledger: `ledger.csv`, one row per unit of migration.
- Recording: a trace captured in Unity and replayed under the shim.
- Snapshot: the per-frame SoA block from C# to Bevy during the rendering phase.
- Host flip: step 10, the first build without a Unity runtime.

---

## 17. References

- decentraland/bevy-explorer: architecture reference; its Bevy fork `robtfm/bevy` branch `release-0.16-dcl` lists the patches a streaming client needs; `cef_offscreen` crate for the HUD texture path; `dcl_deno_ipc` for an out-of-process runtime pattern.
- EggyStudio/BevyCSharp: C# on Bevy with components in Bevy tables, exclusive C# systems, source-generated wrappers; measured 16 to 28 ns per bridge call. Design reference for `crates/host` and the shim threading model.
- Cysharp/csbindgen: Rust to C# FFI generation.
- netcorehost: hostfxr wrapper for `crates/host`.
- BluetufieBugado/unirecomp-tool: post-mortem on UnityEngine reimplementation without an oracle; its SerializedFile and layout tools.
- V-Sekai/unidot_importer: prefab and material translation reference for the exporter.
- Internal: `docs/architecture-overview.md`, `docs/systems.md`, `docs/scene-runtime.md`, `docs/avatar-rendering.md`, `docs/asset-bundles-conversion.md`, `docs/uuav.md`.
