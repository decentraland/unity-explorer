# Local asset bundles via the abgen sidecar (`--local-ab`)

**Branch:** `feat/local-ab-inproc-build` · **Status:** sidecar + E2E test green on Windows (v16, Unity 6000.4.0f1)

## Architecture

`AbgenSidecar` (Global/Dynamic) runs the abgen JIT server as a supervised localhost child:
free loopback port, env-configured (preview content server as `ABGEN_CATALYST_URL`,
`ABGEN_UPSTREAM_AB_CDN` read-through, `ABGEN_JIT_CONTENT_DIGEST=1` so edits reconvert under the
preview server's path-derived hashes, persistent disk cache under `persistentDataPath/abgen-lsd`),
health-checked, restarted up to 3× on unexpected exit, killed in `MainSceneLoader.Shutdown()`.
It only spawns in local scene development with `--local-ab`, and only when no explicit
`--optimized-assets-url` is given. Its base URL becomes the optimized-assets source
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

The binary is never embedded in the build. On first run the **pinned release** is downloaded in
the background into `persistentDataPath/abgen/bin/{version}/`, verified against its compile-time
sha256, and activates on the next launch. Only the pinned version is ever executed — upgrading
abgen requires a deliberate pin+checksum bump in `AbgenSidecar`, so a compromised GitHub release
cannot propagate to users on its own. `StreamingAssets/abgen(.exe)` acts as an explicit developer
override when no pinned install exists.

## Verification

- `AbgenSidecarShould` — spawns the sidecar exactly as MainSceneLoader does, JIT-converts a real
  2-GLB scene via the manifest lane, fetches a bundle through `UnityWebRequestAssetBundle` (the
  client's real consumption path) and verifies Dispose kills the listener. Green on v16 (14.9s).
