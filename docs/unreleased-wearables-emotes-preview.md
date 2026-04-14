# Unreleased Wearables & Emotes Preview

> See also: [Avatar Rendering](avatar-rendering.md) | [Emotes](emotes.md) | [App Arguments](app-arguments.md)

---

## Overview

The Builder preview feature lets creators test wearables and emotes they are building in the Decentraland Builder (web tool) **before** publishing them to the blockchain. Because these items have no on-chain record yet, they cannot be fetched through the standard Lambdas/Catalog endpoint. Instead, the Explorer calls the Builder API directly, using a signed request to prove the caller's identity.

The feature is activated via the `--self-preview-builder-collections` CLI flag, which accepts one or more comma-separated Builder collection UUIDs.

---

## App Arguments

Three related flags live in `AppArgsFlags.cs`:

| Flag | Description |
|------|-------------|
| `self-preview-builder-collections` | Main flag. Fetches unreleased wearables **and** emotes from the Builder API by collection UUID(s). Requires signed requests. |
| `self-preview-wearables` | Simpler flag. Accepts comma-separated URNs of already-published wearables to force-equip. No signing required. |
| `self-preview-emotes` | Simpler flag. Accepts comma-separated URNs of already-published emotes to force-equip. No signing required. |

---

## Builder API Signing

Builder API endpoints for unreleased collections are protected and require proof of the caller's wallet identity. The signing flow:

1. Explorer calls `IWebRequestController.SignedFetchGetAsync()` — defined in `SignedWebRequestControllerExtensions.cs`.
2. A Unix timestamp (`DateTime.UtcNow.UnixTimeAsMilliseconds()`) is used as a temporal nonce.
3. The timestamp is combined with the user's wallet identity into a `WebRequestSignInfo` struct and injected as `Authorization` headers on the outgoing request.
4. Builder API validates that the signature is recent (within an accepted time window) and matches the caller's identity.
5. On success, the API returns the collection's item metadata as JSON.

---

## Unreleased Wearables Preview Architecture

```
CLI: --self-preview-builder-collections <uuid1>,<uuid2>
         ↓
DynamicWorldContainer.CreateAsync()
  Reads DecentralandUrl.BuilderApiDtos
    → https://builder-api.decentraland.{ENV}/v1/collections/[COL-ID]/items
  Reads DecentralandUrl.BuilderApiContent
    → https://builder-api.decentraland.{ENV}/v1/storage/contents/
  Wraps ECSWearablesProvider inside ApplicationParametersWearablesProvider
         ↓
ApplicationParametersWearablesProvider.GetTrimmedByParamsAsync()
  - Detects SELF_PREVIEW_BUILDER_COLLECTIONS flag
  - For each collection UUID: substitutes [COL-ID] in URL template
  - Calls source.GetTrimmedByParamsAsync() with needsBuilderAPISigning: true
         ↓
GetTrimmedWearableByParamIntention
  - NeedsBuilderAPISigning = true
  - CommonArguments.URL = builder URL with resolved collection ID
         ↓
LoadTrimmedWearablesByParamSystem  (inherits LoadTrimmedElementsByIntentionSystem)
  - Detects NeedsBuilderAPISigning
  - Calls webRequestController.SignedFetchGetAsync()
      → adds timestamp + signature headers
  - Parses response as BuilderWearableDTO.BuilderLambdaResponse (Newtonsoft JSON)
  - Calls LoadBuilderItem(): BuildElementDTO(builderContentURL) per item
      → Maps contents dict → ContentDefinition[]
      → Sets assetBundleManifestVersion = AssetBundleManifestVersion.CreateLSDAsset()
         (raw GLTF, NOT an asset bundle)
         ↓
FinalizeRawWearableLoadingSystem  [only injected when builderCollectionsPreview = true]
  - Finalizes GLTF-based wearable loading pipeline
```

### Key files

| File | Role |
|------|------|
| `ApplicationParametersWearablesProvider.cs` | Intercepts wearable requests, applies Builder collection UUIDs, sets signing flag |
| `GetTrimmedWearableByParamIntention.cs` | ECS intention component; carries `NeedsBuilderAPISigning` flag and resolved URL |
| `LoadTrimmedWearablesByParamSystem.cs` | Picks up intentions, calls signed fetch, parses `BuilderLambdaResponse` |
| `LoadTrimmedElementsByIntentionSystem.cs` | Base system shared by wearable and emote loaders |
| `SignedWebRequestControllerExtensions.cs` | `SignedFetchGetAsync()` extension — builds auth headers |
| `WearableDTO.cs` | Defines `BuilderWearableMetadataDto` and `BuildElementDTO()` |
| `FinalizeRawWearableLoadingSystem.cs` | Completes GLTF-based wearable loading (Builder-only path) |
| `DecentralandUrlsSource.cs` | Defines `BuilderApiDtos` and `BuilderApiContent` URL templates |

---

## Unreleased Emotes Preview Architecture

The emote path follows the same pattern as wearables with emote-specific types at each layer:

```
CLI: --self-preview-builder-collections <uuid1>,<uuid2>
         ↓
ApplicationParamsEmoteProvider.GetTrimmedByParamsAsync()
  - Same flag detection and URL resolution
  - needsBuilderAPISigning: true
         ↓
GetTrimmedEmotesByParamIntention  (NeedsBuilderAPISigning = true)
         ↓
LoadTrimmedEmotesByParamSystem
  - SignedFetchGetAsync() (same signing mechanism)
  - Parses BuilderEmoteDTO.BuilderLambdaResponse
  - Calls LoadBuilderItem(): BuildElementDTO(builderContentURL)
         ↓
ResolveBuilderEmotePromisesSystem  [only injected when builderCollectionsPreview = true]
  - For each emote in results with ContentDownloadUrl set:
      .glb file  → creates GetGLTFIntention
                   → World.Create(gltfPromise, emote, bodyShape)
      .mp3/.ogg  → creates AudioClipPromise via AudioUtils
                   URL = ContentDownloadUrl + content hash
  - Marks as StreamableResult when all promises resolve
```

### Key files

| File | Role |
|------|------|
| `ApplicationParamsEmoteProvider.cs` | Intercepts emote requests, applies Builder collection UUIDs, sets signing flag |
| `GetTrimmedEmotesByParamIntention.cs` | ECS intention component; carries `NeedsBuilderAPISigning` flag and resolved URL |
| `LoadTrimmedEmotesByParamSystem.cs` | Picks up intentions, calls signed fetch, parses `BuilderLambdaResponse` |
| `EmoteDTO.cs` | Defines `BuilderEmoteMetadataDto` and `BuildElementDTO()` |
| `ResolveBuilderEmotePromisesSystem.cs` | Resolves GLTF and audio promises for Builder emotes |
| `EmotePlugin.cs` | Contains the conditional injection guard for Builder-only systems |

---

## Self-Preview via URN (`self-preview-wearables` / `self-preview-emotes`)

These two flags provide a simpler, signing-free alternative for items that are already published:

- Accept comma-separated URN identifiers (e.g. `urn:decentraland:...`).
- Bypass the Builder API entirely — items are fetched through the standard Lambdas/Catalog endpoint.
- No signing required because the items exist on-chain.
- **Items must already be published** (have a valid URN). These flags cannot preview unpublished items.
- Typical use case: force-equip specific published items on yourself for QA or demo purposes without modifying your profile.

---

## How to Test Previewing Unreleased Wearables/Emotes

### Setup — Wearables

1. Open the Decentraland Builder wearables section at `https://decentraland.org/builder/collections/`.
2. Create 2 collections and upload the provided test assets (split half in each collection):
   [TestWearables.zip](https://github.com/user-attachments/files/18674528/TestWearables.zip)
3. Record the collection UUID(s) from the browser URL (format: `3062136a-065d-4d94-b28c-f57d6ef04860`).

### Setup — Emotes

1. Open the Decentraland Builder emotes section at `https://decentraland.org/builder/collections/`.
2. Create 2 collections and upload the provided test emotes (2 per collection):
   [test-emotes-props.zip](https://github.com/user-attachments/files/20511294/test-emotes-props.zip)
   - One emote (`lovegrenadewithsound`) includes audio — upload its `.zip` file directly so all files are included.
3. Record the collection UUID(s) from the browser URL.

### Launching

Deep link example (paste on any web browser, uses the latest released installed Explorer): `decentraland://?position=100,100&self-preview-builder-collections=a2041268-189e-4cef-902d-70272aed077c`

Terminal example:
```bash
# macOS — wearables or emotes (same flag)
open Decentraland.app --args --debug --self-preview-builder-collections <UUID-1>,<UUID-2>

# Windows
Decentraland.exe --debug --self-preview-builder-collections <UUID-1>,<UUID-2>
```

### Verification checklist

- Items from all collections appear in the Backpack.
- Items can be equipped and unequipped.
- Emotes play correctly on the avatar (including audio where present).
- Re-launching without the flag restores normal behaviour (no ghost items).
- Injecting a single UUID shows only that collection's items (negative test).
