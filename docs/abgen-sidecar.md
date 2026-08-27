# Local asset bundles via the abgen sidecar (`--local-ab`)

**Branch:** `feat/local-ab-inproc-build` · **Pinned:** abgen v0.17.8 · **Status:** editor E2E green
on Windows (v16, Unity 6000.4.0f1); macOS player verified end-to-end; Windows player pending re-test
after the CPU-encoder pin.

## Architecture

`AbgenSidecar` (Global/Dynamic) runs the abgen JIT server as a supervised localhost child on
abgen's **default bind** (`127.0.0.1:5147`) — the endpoint is deliberately not exported, because
abgen's `HTTP_SERVER_HOST`/`HTTP_SERVER_PORT` are generic names that would leak into every child
process spawned afterwards; a second `--local-ab` instance loses the port and its scene degrades
to raw GLTFs. Everything else is env-configured (preview content server as `ABGEN_CATALYST_URL`,
`ABGEN_UPSTREAM_AB_CDN` read-through, `ABGEN_JIT_CONTENT_DIGEST=1` so edits reconvert under the
preview server's path-derived hashes, persistent disk cache under `persistentDataPath/abgen-lsd`,
and `ABGEN_GPU_BACKEND=off` — see below), health-checked, restarted up to 3× on unexpected exit.

The lifecycle is owned end to end by `MainSceneLoader` and is deliberately **serial where it
matters**: in local scene development with `--local-ab` (and no explicit `--optimized-assets-url`),
**`AbgenSidecarBootstrap`** is constructed and its `StartAsync` *awaited under the splash screen,
before the URL sources are built*. It resolves the realm directly from the launch settings (LSD is
only entered with a web-scheme realm param or the editor's Localhost preset), downloads the pinned
binary when absent (`EnsurePinnedBinaryAsync` — the AB panel is auto-opened so the wait is
visible), creates the sidecar (`TryCreate`), launches it and polls it to health (`StartAsync`).
Only when it returns `true` — the server is provably serving — is its base URL passed into the URL
sources as the optimized-assets override; on `false` the override is simply never set and the
session is byte-for-byte production, with no correction step anywhere. The whole-scene warm-up
then runs in the background (`WarmUpTask`), overlapping the rest of boot. The child is killed on
dispose (`MainSceneLoader.Shutdown()`, registered as a quit-cleanup candidate). In every other
mode no sidecar object is ever constructed.

The sidecar's base URL becomes the optimized-assets source
(`AssetBundlesCDN` / `LodGeneratorCDN` / `AssetBundleRegistry`): the server JIT-converts the local
scene and answers everything else — wearables, emotes, LODs, registry records — from the
production upstream via its built-in ab-cdn read-through and registry pass-through, so no lane
loses content. The loading flow is untouched: bundles stream over loopback via
`DownloadHandlerAssetBundle` directly into native memory (no managed copies), requested on the
standard v25+ hash-in-path lane (`{version}/{sceneID}/{hash}`). Deps digests are skipped in LSD
(`SceneAssetBundleDigestsLoader` takes `isLocalSceneDevelopment`): LSD hashes are path-derived and
unique per file, so the collisions digests disambiguate cannot occur, and skipping the second
manifest download removes ~3 s of scene-entry latency against a JIT-revalidating server.

At boot the warm-up (`WarmUpLocalSceneAsync`) resolves the scene entity from the realm (`/about`
`scenesUrn`, falling back to `localSceneParcels` + `POST /entities/active`) and requests its
manifest, which makes abgen convert the whole scene in one pass — parallelized across files since
v0.16.0 (bounded file-level workers overlap one file's single-threaded fetch/parse/BC5/IO
phases with another file's core-parallel BC7 encode; `ABGEN_JIT_FILE_CONCURRENCY` caps the workers);
`/progress/{entity}` is polled into `AbgenConversionMetrics` for the AB Conversion debug panel.

**CPU encoder pin**: with no backend pinned abgen auto-tries its GPU BC7/BC5 encoder, and arming
CUDA costs ~60 s before the HTTP listener binds (measured on an RTX 3060: 63.6 s/59.0 s unpinned,
1.03 s with `ABGEN_GPU_BACKEND=off`) — far past the health timeout, so the sidecar was killed
before it ever answered. The CPU encoder is the reference the GPU path is qualified against, so
bundles stay byte-identical; the pin trades encode throughput for a usable startup.

**IL2CPP-safe process control**: `System.Diagnostics.Process` cannot spawn in player builds
(Win32Exception "Native error= Success"), so the child is held as a raw OS handle/pid — Windows
uses `CreateProcessW` with `CREATE_NO_WINDOW`, macOS/Linux the `DclProcesses` native plugin
(`posix_spawnp`) plus libc `kill`/`chmod`; liveness is a poll (`WaitForSingleObject` /
`kill(pid, 0)`), not the managed `Exited` event. Only the editor keeps the managed `Process`
path (with drained stdout/stderr pipes).

Measured (Linux x86_64, CPU encoder): cold whole-entity JIT 0.8s (2-GLB scene) / 5.3s (24-GLB,
12MB); warm disk-cache hits <1ms; server RSS ~16MB idle, 130–435MB peak during converts. v0.16.0's
per-file parallelization cut cold whole-scene conversion by ~30 s on an M-series Mac against a real
scene (the single-threaded valleys between BC7 bursts now overlap); peak RSS runs higher since up to
`ABGEN_JIT_FILE_CONCURRENCY` files (default `min(4, cores)`) decode concurrently.

## Binary acquisition

The binary is never embedded in the build. On first run `AbgenSidecarBootstrap` downloads the
**pinned release** into `persistentDataPath/abgen/bin/{version}/`, verified against its
compile-time sha256, then starts the sidecar in the same session; download progress lands in the
AB panel as milestone rows (25% steps). Only the pinned version is ever executed — upgrading
abgen requires a deliberate pin+checksum bump in `AbgenSidecar`, so a compromised GitHub release
cannot propagate to users on its own. `StreamingAssets/abgen(.exe)` acts as an explicit developer
override when no pinned install exists.

**Boot holds on the warm-up**: the scene's bundles-vs-GLTFs verdict is made once, at the scene's
first manifest request, and a failure is cached for the session (`IrrecoverableFailures`). So
`MainSceneLoader` awaits `AbgenSidecarBootstrap.WarmUpTask` before loading the starting realm
(which is what starts scene loading), so the first manifest request lands on converted bundles
rather than a server mid-conversion; the warm-up's outcome carries no signal (a healthy server
JIT-converts per request regardless), and the task is pre-completed when the server never came
up. First run therefore enters the world with bundles already served; outside LSD + `--local-ab`
no sidecar exists and boot is unaffected. Both waits — the serial health launch and this hold —
are absorbed under the splash screen, before the authentication screen.

## Visibility

The scene dev console's AB tab mirrors `/progress/{entity}` live: the summary shows the server's
authoritative `converted/total` counter, per-file rows show whatever the 500 ms poll catches
(backfilled to the full census when the manifest lands), and milestone rows mark every lifecycle
moment — download progress, installed, warm-up started, READY in Ns, already-warm, server-side
failures (manifest exitCode), sidecar failed. The sidebar AB button pulses while conversion runs
and stays lit after unseen failures. The panel **opens itself** when long-running work starts
(consumable open-request on `AbgenConversionMetrics`, consumed by `DebugMenuController`) and
**closes itself** a few seconds after a clean READY — any failure keeps it open, and a manual
toggle cancels the automation. A CLEAR CACHE button empties the local bundle cache; a HIGHLIGHT
SOURCES toggle tints scene objects green (bundle-sourced) or red (raw-GLTF roots).

## Verification

- `AbgenSidecarShould` — reserves and starts the sidecar exactly as the production flow does, JIT-converts a real
  2-GLB scene via the manifest lane, fetches a bundle through `UnityWebRequestAssetBundle` (the
  client's real consumption path) and verifies Dispose kills the listener. Green on v16 (14.9s).
- macOS player (M-series): full first-run flow — download, install, launch, whole-scene
  conversion, bundles served — verified by QA/dev testing on this branch.
