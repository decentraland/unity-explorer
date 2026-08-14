# Local asset bundles via the abgen sidecar (`--local-ab`)

**Branch:** `feat/local-ab-inproc-build` · **Pinned:** abgen v0.16.0 · **Status:** editor E2E green
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

The lifecycle is split in two. `MainSceneLoader` only *reserves the loopback endpoint*
(`AbgenSidecar.ReserveBaseUrl` — a URL, nothing else) — it must exist before the URL sources are
built — and only in local scene development with `--local-ab` when no explicit
`--optimized-assets-url` is given. Everything else lives in **`AbgenSidecarPlugin`**
(PluginSystem/Global), registered by `DynamicWorldContainer` exclusively from that reserved URL:
it resolves the realm through the canonical `RealmUrls.LocalSceneDevelopmentRealmAsync`, downloads
the pinned binary when absent (`EnsurePinnedBinaryAsync`, awaited — the AB panel is auto-opened so
the wait is visible), creates the sidecar (`TryCreate`), launches it
(`StartAsync`), runs the whole-scene warm-up once healthy, and kills the child on dispose
(global-plugin teardown in `MainSceneLoader.Shutdown()`). In every other mode the plugin is never
constructed.

The sidecar's base URL becomes the optimized-assets source
(`AssetBundlesCDN` / `LodGeneratorCDN` / `AssetBundleRegistry`): the server JIT-converts the local
scene and answers everything else — wearables, emotes, LODs, registry records — from the
production upstream via its built-in ab-cdn read-through and registry pass-through, so no lane
loses content. The loading flow is untouched: bundles stream over loopback via
`DownloadHandlerAssetBundle` directly into native memory (no managed copies), requested on the
standard v25+ hash-in-path lane (`{version}/{sceneID}/{hash}`). Deps digests are skipped in LSD
(`SceneAssetBundleDigestsLoader` takes `isLocalSceneDevelopment`): LSD hashes are path-derived and
unique per file, so the collisions digests disambiguate cannot occur, and skipping the second
manifest download removes ~3 s of scene-entry latency against a JIT-revalidating server. The scene
lane's own manifest request is also served from a hand-off (`AbgenManifestPrewarm`): the warm-up
(and, after edits, the reconversion watcher) stores the response it already awaited, keyed by the
exact URL, and `LoadAssetBundleManifestSystem` reuses it instead of paying the server's content
revalidation again — several more seconds on a large scene. A content edit invalidates the entry
(the census may have changed), so a racing reload falls back to a fresh fetch.

At boot the warm-up (`WarmUpLocalSceneAsync`) resolves the scene entity from the realm (`/about`
`scenesUrn`, falling back to `localSceneParcels` + `POST /entities/active`) and requests its
manifest, which makes abgen convert the whole scene in one pass — parallelized across files as of
the pinned v0.16.0 (bounded file-level workers overlap one file's single-threaded fetch/parse/BC5/IO
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

**Orphan protection**: teardown is cooperative (Dispose kills the child), so a hard crash of the
explorer used to leave the server running — and with the fixed default port an orphan *owns* the
endpoint the next session expects. Two defenses: on Windows (player and editor) every child is
assigned to a kill-on-close Job Object, so the kernel reaps it when the explorer dies for any
reason; and `StartAsync` refuses to adopt a foreign listener — after the health check passes it
verifies our own child is still alive, and if the port is answered by anything else (orphan from a
crashed macOS session, unrelated process) it fails fast with an explicit milestone instead of
silently serving stale bundles. macOS has no job-object equivalent; a parent-pid watchdog in abgen
is the tracked upstream complement.

Measured (Linux x86_64, CPU encoder): cold whole-entity JIT 0.8s (2-GLB scene) / 5.3s (24-GLB,
12MB); warm disk-cache hits <1ms; server RSS ~16MB idle, 130–435MB peak during converts. v0.16.0's
per-file parallelization cut cold whole-scene conversion by ~30 s on an M-series Mac against a real
scene (the single-threaded valleys between BC7 bursts now overlap); peak RSS runs higher since up to
`ABGEN_JIT_FILE_CONCURRENCY` files (default `min(4, cores)`) decode concurrently.

## Binary acquisition

The binary is never embedded in the build. On first run `AbgenSidecarPlugin` downloads the
**pinned release** into `persistentDataPath/abgen/bin/{version}/`, verified against its
compile-time sha256, then starts the sidecar in the same session; download progress lands in the
AB panel as milestone rows (25% steps). Only the pinned version is ever executed — upgrading
abgen requires a deliberate pin+checksum bump in `AbgenSidecar`, so a compromised GitHub release
cannot propagate to users on its own. `StreamingAssets/abgen(.exe)` acts as an explicit developer
override when no pinned install exists.

**Boot holds on readiness**: the scene's bundles-vs-GLTFs verdict is made once, at the scene's
first manifest request, and a failure is cached for the session (`IrrecoverableFailures`). So
`MainSceneLoader` awaits `DynamicWorldContainer.AbgenSidecarReadyAsync` before loading the
starting realm (which is what starts scene loading): the plugin completes it when the sidecar is
warm — whole-scene warm-up done — or has given up (no binary and the download failed, launch
failure). First run therefore enters the world with bundles already served; outside
LSD + `--local-ab` the task is pre-completed and boot is unaffected. The wait is absorbed under
the splash screen, before the authentication screen.

**Clean fallback when the sidecar can't be had**: the readiness task resolves to a bool — false
when the server never came up — and `MainSceneLoader` then drops the optimized-assets override
(`DecentralandUrlsSource.ClearOptimizedAssetsOverride`, which also evicts the flag-dependent URL
cache) before any optimized-asset request has resolved. The whole session — scene bundles,
wearables, emotes, LODs, and the registry-composed profile/entities endpoints — falls back to the
production hosts exactly as if `--local-ab` had not been passed, instead of hitting the dead
loopback port and recovering per request. A server that turns healthy and dies later keeps the
override (bundles JIT per request; supervision restarts it up to 3×).

## Visibility

The scene dev console's AB tab mirrors `/progress/{entity}` live: the summary shows the server's
authoritative `converted/total` counter, per-file rows show whatever the 500 ms poll catches
(backfilled to the full census when the manifest lands), and milestone rows mark every lifecycle
moment — download progress, installed, warm-up started, READY in Ns, already-warm, server-side
failures (manifest exitCode), sidecar failed. Content-edit reconversions are mirrored too: the LSD
reload path (`LocalSceneDevelopmentController`, which already receives the preview server's edit
message — including the changed model's path) raises a consumable signal on
`AbgenConversionMetrics`; the sidecar's session-long watcher (`WatchReconversionsAsync`) consumes it
and re-runs the manifest lane, which coalesces with (or triggers) the server's rebuild — the panel
flips back to converting, names the edited file, tracks the rebuild and settles to READY with a
"reconverted in Ns" milestone, accurate even when the rebuild outpaces the progress poll (texture
edits arrive as unnamed whole-scene updates — sdk-commands only names `.glb/.gltf` changes).
The sidebar AB button pulses while conversion runs
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
