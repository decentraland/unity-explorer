# Local scene development: in-process asset-bundle build (`--local-ab`)

**Branch:** `feat/local-ab-inproc-build` (prototype) · builds on `feat/ab-local-fallback-abgen`

## What it is

`--local-ab` **is** the in-process asset-bundle build. It replaces the old meaning of the flag
(fetch prebuilt bundles from the preview server's abgen sidecar at `{realm}/optimized-assets`) —
there is now a single local-ab mode and no sidecar/server involved. The LSD asset lanes are:

| Mode | Flag | Where bundles come from |
| --- | --- | --- |
| Raw GLTFs (default) | — | No bundles; GLTFs load directly from the preview content server |
| Remote bundles | `--lsd-use-remote-ab` | The production CDN (hybrid scene) |
| **In-process build** | `--local-ab` | **The client converts them itself, cached on disk** |

With `--local-ab` the explorer fetches the scene's source GLTFs and their external
textures/buffers from the preview content server (`ISceneContent`, the same origin the raw-GLTF
lane uses) and converts each model to a Unity AssetBundle **in-process** with the embedded abgen
native library (`org.decentraland.abgen`, ConvertOnly mode). Results are written to an on-disk
cache, so a `/reload` or a next-day restart reconverts only the assets whose bytes changed. No SDK
sidecar, no `/optimized-assets` proxy, no manifest endpoint — the only server involved is the
content server the preview already runs.

## Flow

1. `RealmLaunchSettings.ParseRealmAppParameter` accepts `--local-ab` (or the *Use Local Asset
   Bundles* Editor checkbox on the Localhost realm) only in local scene development; the Editor
   checkbox is Editor-only, player builds opt in exclusively through the arg. Precedence over
   `--lsd-use-remote-ab`.
2. Scene-definition loading stamps the **manual manifest**
   (`AssetBundleManifestVersion.CreateManualManifest()`) in all of LSD — no manifest is fetched
   from anywhere.
3. `PrepareGltfAssetLoadingSystem` keeps the asset-bundle lane instead of degrading to raw GLTFs:
   it emits `GetAssetBundleIntention` with the manual manifest (`Hash` is the platform-suffixed
   path-derived hash — a cache key only, never a URL).
4. `LoadAssetBundleSystem` (scene worlds only; AND-ed with `LaunchMode.LocalSceneDevelopment` in
   `AssetBundlesPlugin`) calls `AbgenAssetBundleFallback.TryBuildAsync` **before** any network
   attempt: fetch GLB → parse external `images[].uri`/`buffers[].uri` → fetch those from the
   content server → check the disk cache → convert (only on a miss) → `AssetBundle.LoadFromFile`.
5. The bundle's `metadata.json` dependencies are the embedded shader bundles
   (`dcl/scene_ignore_{platform}`, …), which resolve through the existing `AssetSource.Embedded`
   path from `StreamingAssets/AssetBundles` — fully offline. Textures are baked into each model
   bundle by ConvertOnly, so there are no texture dependency bundles to miss.
6. Failures degrade per-asset: if the build returns null the normal web lane runs (and dead-ends
   against the CDN with the path-derived hash), surfacing the usual "Asset Bundle is null" error
   for that entity only.

## On-disk cache (`AbgenBundleDiskCache`)

- Root: `{persistentDataPath}/abgen-bundles/v1/`, two-char sharded.
- Key: `SHA256(AbgenRequest.ToBytes() ++ abgenVersion)`. The request blob carries the GLB and every
  dependency's bytes plus the platform, so the key changes exactly when a source file, a
  dependency, the platform or the converter changes — and never otherwise. This is **not** abgen's
  own deps digest, which is computed over the content-table hashes; those are path-derived in LSD
  and never change on edit (the reason `ignoreCacheHash` exists).
- Hit → `AssetBundle.LoadFromFile` (memory-mapped), conversion skipped entirely.
- Miss → convert on the thread pool, write atomically (temp + `File.Move`), then `LoadFromFile`.
- Survives `/reload` and full restarts; only edited assets reconvert.

## Eager warmup

`WarmUpLocalAssetBundlesSystem` (scene worlds, this mode only) pre-issues a
`GetAssetBundleIntention` promise for every `.glb`/`.gltf` in the scene content at
`PartitionComponent.MIN_PRIORITY`, so the whole scene converts in the background while it loads.
Direct requests stay ahead in the loading-budget queue and piggyback on in-flight warmup loads
through the streamable cache (intents are keyed by hash), so nothing converts twice. Outstanding
promises are forgotten on world teardown (`IFinalizeWorldSystem`).

## AB Conversion panel (scene dev console)

The scene dev console (`--scene-console true`, shown automatically in LSD) has an **AB** sidebar
button opening the "AB Conversion" panel: a summary line (planned / converting / ok / failed /
total MB) and one row per conversion (status, size, duration, artifact — disk hits show `(disk)`
and 0 ms), newest first. Fed by `AbgenConversionMetrics.INSTANCE`, written from the conversion flow
(`AbgenAssetBundleFallback`) on worker threads; the warmup resets it per scene session.

## How to enable

- **Editor:** Main scene → `RealmLaunchSettings` → Initial Realm `Localhost` → tick *Use Local
  Asset Bundles* (with `dcl start` running).
- **Player build / deep link:** append `&local-ab=true` to an LSD deep link (`local-scene=true`,
  loopback realm). Allow-listed for whitelisted/loopback realms only (`DeepLinkAllowlist`).
- The native library must be present in the package
  (`Explorer/Packages/org.decentraland.abgen/Runtime/Plugins/<platform>/`); build it from the
  abgen repo with `cargo build --release -p abgen-native`.

## Known limitations (prototype)

- Scene worlds only: the global world (wearables/emotes) passes no `ISceneContent`, so it keeps
  its normal lanes.
- No cache eviction yet: `{persistentDataPath}/abgen-bundles` grows unbounded across sessions
  (bump `CACHE_VERSION` or delete the folder to clear). A size-capped LRU is the next increment.
- The source bytes are re-fetched from the content server on every load to compute the cache key
  (cheap over loopback); only conversion — the expensive part — is skipped on a hit.
- The conversion is CPU-bound on the client (native pool capped at 2–4 threads); a cold heavy
  scene converts noticeably slower than the warmed SDK sidecar, but with zero external processes
  and no cost after the first build.

## Files touched (on top of `feat/ab-local-fallback-abgen`)

- `AppArgsFlags` / `DeepLinkAllowlist`: `--local-ab` re-documented as the in-process build
- `RealmLaunchSettings` (+ drawer): `useLocalAssetBundles` now drives the build; server-URL
  derivation removed
- `MainSceneLoader`: local-ab no longer points at a server; sidecar not started in this mode
- `LoadSceneDefinitionSystem` / `…ListSystem`: manual manifest always in LSD
- `GltfContainerPlugin` / `PrepareGltfAssetLoadingSystem`: keep the AB lane on the manual manifest
- `AssetBundlesPlugin` / `LoadAssetBundleSystem`: in-process build before the network
- `AbgenAssetBundleFallback` (+ `AbgenBundleDiskCache`): fetch → key → disk hit or convert+persist
- `WarmUpLocalAssetBundlesSystem`, `AbgenConversionMetrics`, `AbConversionPanelView`: eager warmup
  + scene-console panel
- Tests: `RealmLaunchSettingsShould.EnableLocalAssetBundlesFromLocalAbFlag`,
  `AppArgsTests` local-ab loopback/remote deep-link tests
