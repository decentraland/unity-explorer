# Local asset-bundle fallback via abgen — design + adversarial audit

**Branch:** `feat/ab-local-fallback-abgen` · **Status:** helper + sidecar + all 6 tests green on Windows (v16, Unity 6000.4.0f1, abgen 0.15.3)

## Sidecar architecture (the shipping shape)

`AbgenSidecar` (Global/Dynamic) runs the abgen JIT server as a supervised localhost child:
free loopback port, env-configured (peer catalyst upstream, `ABGEN_UPSTREAM_AB_CDN` read-through,
persistent disk cache under `persistentDataPath/abgen`), health-checked, restarted up to 3× on
unexpected exit, killed in `MainSceneLoader.Shutdown()`. It only spawns in local scene development
with `--local-ab`, and only when no explicit `--optimized-assets-url` is given. Its base URL feeds
`DecentralandUrl.SceneAssetBundlesCDN` exclusively — scene GLB bundles and scene manifests — while
wearables, emotes, LODs and the asset-bundle registry keep resolving to their dedicated hosts. The
loading flow is untouched: bundles stream over loopback via `DownloadHandlerAssetBundle` directly
into native memory (no managed copies), the server JIT-converting whatever its cache lacks. The
in-proc fallback below remains as a second, narrower safety net; it can be stripped once the
sidecar is judged sufficient.

Measured (Linux x86_64, CPU-only): cold whole-entity JIT 0.8s (2-GLB scene) / 5.3s (24-GLB, 12MB);
warm disk-cache hits <1ms; server RSS ~16MB idle, 130–435MB peak during converts; GPU BC7/BC5
encode auto-tries CUDA→wgpu and falls back to CPU with a logged qualification verdict.

When `LoadAssetBundleSystem` gets no bundle from the CDN, `AbgenAssetBundleFallback` fetches the
source model and its external dependencies from the scene's content server and converts them to a
Unity AssetBundle in-process with the embedded abgen native library (`org.decentraland.abgen`,
ConvertOnly mode). No Editor, no sidecar process, no HTTP server.

## Resolution contract (must mirror abgen's `crate/src/naming.rs`)

`resolve_uri_to_content_file(uri, glb_file)`: reject empty / scheme (`[A-Za-z][A-Za-z0-9+.-]*:`) /
protocol-relative (`//`) / absolute (`/`) / query-fragment (`?#`) URIs → percent-decode (invalid
escapes pass through) → posix-join to `dirname(glb_file)` → normpath → reject `../` root escape →
lowercase → look up in the lowercased content map. The C# side approximates this with a
`System.Uri` combine + case-insensitive `ISceneContent` lookup.

## Adversarial audit (2026-08-04)

Two independent checks:

1. **Source audit** of abgen 0.15.3 (`naming.rs`, `gltf/load.rs`, `gltf/scene_build.rs`,
   `export/convert.rs`, `builder/*`), 12 targeted questions.
2. **Corpus sweep**: every active scene on the catalyst (25,003 entities, 163,376 GLB references,
   63,167 external glTF URIs), each URI resolved with a faithful Python port of abgen's algorithm
   AND a model of the C# helper's `System.Uri` semantics, then diffed.

**Result: 0 of 63,167 corpus URIs resolve differently between abgen and the helper.**

Corpus shape: 61,512 resolve to a mapped content file; 1,652 reference files missing from the scene
mapping; 3 carry Windows-absolute paths (`C:\Users\...`, a real Genesis robot GLB); 133 contain
spaces, 25 non-ASCII, 6 percent-escapes, 2 legitimate `../` traversals, 24 GLBs list a duplicate
URI; 76 GLBs are corrupt (bad magic / short / unparseable JSON / JSON not chunk 0); 19,836 models
are `.gltf` (JSON) rather than `.glb`. All external URIs are images — no scene `.glb` uses external
buffers.

Audit findings folded into the helper:

- **Missing references are soft.** abgen emits the bundle with the texture slot empty when an image
  URI doesn't resolve (missing *buffers* hard-fail that one model inside abgen). The helper
  therefore skips unresolvable URIs instead of aborting — matching what the CDN pipeline produced
  for those 1,652 real cases.
- **`.gltf` is supported** by abgen (suffix dispatch, `GLTF_EXTENSIONS = [".glb", ".gltf"]`), so
  the helper accepts both and parses `.gltf` bytes as raw glTF JSON.
- **Duplicate URIs are deduplicated** before fetching (abgen tolerates duplicates — last-writer
  content map — but double-fetching is waste).
- Scheme'd URIs (incl. `C:\...`): helper skips fetching; abgen soft-skips the texture. Same bundle
  either way.
- Only `images[].uri` and `buffers[].uri` are read — abgen ignores extension-declared sources
  (`KHR_texture_basisu` etc.); the corpus contains zero URIs outside those two arrays.

Known theoretical divergences, all benign on this corpus (0 hits): percent-encoded leading slash
(`%2F...`) slips abgen's absolute-path guard but fails the map lookup either way; `System.Uri`
clamps root-escaping `../` where abgen bails (both end in a failed conversion); the helper's
`Contains(':')` scheme test is stricter than abgen's for a `:` after a `/`.

## Known gaps (deliberate, next increments)

- **Dependency bundles**: texture/dependency AB intents carry no `Name`, so a missing *texture
  bundle* can't fall back yet — only the model bundle. Full coverage needs a hash→file reverse
  lookup over `SceneEntityDefinition.content`.
- **Global world** (wearables/emotes): `LoadGlobalAssetBundleSystem` passes a null `ISceneContent`;
  fallback is scene-only for now.
- `OnlyGlb` matches the file name **case-sensitively** in abgen while everything else is
  case-insensitive — the helper passes `intention.Name` verbatim into both the request files and
  `OnlyGlb`, so they can't drift.

## Verification

- `AbgenNativeSmokeShould` — ABI + version of the embedded static dll.
- `AbgenFallbackFullFlowShould` — production helper end-to-end from only (entity id, GLB path):
  discovery → resolution → fetch → convert → load, for a 1-texture and a 3-texture model. Green on
  v16.
- `AbgenFallbackVisualShould` — renders the abgen-built airdrop bundle AND the real CDN bundle of
  the same model through identical client mechanics (metadata.json deps from CDN + the embedded
  scene shader bundle) and photographs both. Green on v16: both fully textured (DCL/Scene shader,
  baseColor bound; ~13k distinct colors each), visually indistinguishable. The abgen artifact name
  equals the production manifest entry including the deps digest, and both bundles reference the
  same shader CAB-51fbd4c9. Gotchas encoded in the test: ConvertOnly needs OnlyGlb (else 0
  artifacts); scene bundles render InternalErrorShader until the embedded shader CAB is resident.
- `AbgenSidecarShould` — spawns the sidecar exactly as MainSceneLoader does, JIT-converts a real
  2-GLB scene via the manifest lane, fetches a bundle through `UnityWebRequestAssetBundle` (the
  client's real consumption path) and verifies Dispose kills the listener. Green on v16 (14.9s).
- Corpus sweep script: session scratchpad `corpus-sweep.py` (+ `summary.json` with per-bucket
  examples).
