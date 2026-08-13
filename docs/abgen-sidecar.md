# Local asset bundles via the abgen sidecar (`--local-ab`)

**Branch:** `feat/local-ab-inproc-build` · **Status:** sidecar + E2E test green on Windows (v16, Unity 6000.4.0f1)

## Architecture

`AbgenSidecar` (Global/Dynamic) runs the abgen JIT server as a supervised localhost child:
free loopback port, env-configured (preview content server as `ABGEN_CATALYST_URL`,
`ABGEN_UPSTREAM_AB_CDN` read-through, `ABGEN_JIT_CONTENT_DIGEST=1` so edits reconvert under the
preview server's path-derived hashes, persistent disk cache under `persistentDataPath/abgen-lsd`),
health-checked, restarted up to 3× on unexpected exit.

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
`DownloadHandlerAssetBundle` directly into native memory (no managed copies).

At boot the warm-up (`WarmUpLocalSceneAsync`) resolves the scene entity from the realm (`/about`
`scenesUrn`, falling back to `localSceneParcels` + `POST /entities/active`) and requests its
manifest, which makes abgen convert the whole scene in one pass; `/progress/{entity}` is polled
into `AbgenConversionMetrics` for the AB Conversion debug panel.

Measured (Linux x86_64, CPU-only): cold whole-entity JIT 0.8s (2-GLB scene) / 5.3s (24-GLB, 12MB);
warm disk-cache hits <1ms; server RSS ~16MB idle, 130–435MB peak during converts; GPU BC7/BC5
encode auto-tries CUDA→wgpu and falls back to CPU with a logged qualification verdict.

## Binary acquisition

The binary is never embedded in the build. On first run `AbgenSidecarPlugin` downloads the
**pinned release** into `persistentDataPath/abgen/bin/{version}/`, verified against its
compile-time sha256, then starts the sidecar in the same session; download progress lands in the
AB panel as milestone rows (25% steps) and the panel opens itself once the debug menu is on
screen — then closes itself a few seconds after a clean READY (failures keep it open, and a
manual toggle cancels the automation). Only the pinned version is ever executed — upgrading abgen requires a deliberate
pin+checksum bump in `AbgenSidecar`, so a compromised GitHub release cannot propagate to users on
its own. `StreamingAssets/abgen(.exe)` acts as an explicit developer override when no pinned
install exists.

**Boot holds on readiness**: the scene's bundles-vs-GLTFs verdict is made once, at the scene's
first manifest request, and a failure is cached for the session (`IrrecoverableFailures`). So
`MainSceneLoader` awaits `DynamicWorldContainer.AbgenSidecarReadyAsync` before loading the
starting realm (which is what starts scene loading): the plugin completes it when the sidecar is
warm — whole-scene warm-up done — or has given up (no binary and the download failed, launch
failure). First run therefore enters the world with bundles already served; outside
LSD + `--local-ab` the task is pre-completed and boot is unaffected.

## Verification

- `AbgenSidecarShould` — reserves and starts the sidecar exactly as the production flow does, JIT-converts a real
  2-GLB scene via the manifest lane, fetches a bundle through `UnityWebRequestAssetBundle` (the
  client's real consumption path) and verifies Dispose kills the listener. Green on v16 (14.9s).
