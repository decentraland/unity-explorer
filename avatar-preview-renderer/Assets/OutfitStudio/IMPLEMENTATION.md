# Outfit Studio — Implementation Documentation

> Developer/handoff doc for the Outfit Studio artist tool. The user-facing workflow doc is
> [README.md](README.md) in this folder. Written 2026-07-13; iteration 2 (edit-mode preview)
> added same day — see §11.

## 1. What this is

An **editor-only artist tool** for Decentraland: browse the live marketplace catalog, compose an
outfit (one wearable per slot) on an avatar, pose it with an emote, and capture high-res PNG
stills and MP4 video. It drives the existing aang-renderer pipeline in **play mode** — it does
not reimplement any loading/rendering.

Menu entry: **Decentraland ▸ Outfit Studio** (`OutfitStudioWindow`, UI Toolkit EditorWindow).

## 2. Repo / branch context

- Built on local branch **`feat/outfit-studio`**, branched off **`main`** (deliberately *not* off
  `feat/avatar-toon-shader-metallic-normals` — kept independent of the in-progress
  metallic/normals shader work).
- All code is folder-isolated under `Assets/OutfitStudio/`. **No asmdefs** — the repo has none,
  so everything compiles into `Assembly-CSharp` / `Assembly-CSharp-Editor` (an asmdef cannot
  reference `Assembly-CSharp`, which is where the renderer classes live). `Editor/` uses Unity's
  special-folder rule to land in the editor assembly.
- Touch points outside this folder (kept minimal for easy extraction later; all behave
  identically in play mode / production builds):
  1. `Packages/manifest.json` — added `"com.unity.recorder": "5.1.2"` (editor-only package).
  2. `Assets/Scripts/Preview/PreviewController.cs` — `ResolveBuilderEmote()` (see §6). Additive.
  3. `Assets/Scripts/Loading/GLTFLoader.cs` — `Sanitize` uses `DestroyImmediate` outside play
     mode (was an edit-mode error; play path unchanged — see §11).
  4. `Assets/Scripts/Services/EntityService.cs` — removed a hard `Assert.AreEqual` that fired
     when the catalyst returns fewer entities than requested (e.g. third-party/linked wearables).
     The graceful shortfall handling below it (warn + return resolved subset) was already there
     and now actually runs. Assertions are stripped from production builds, so prod is unchanged.
  5. `Assets/Scripts/Preview/PreviewUIPresenter.cs` — the debug URL presets list was hoisted
     from a local variable in `EnableDebug()` to `public static readonly DEBUG_URL_PRESETS`
     (same content) so the window's Debug tab shares one source of truth. Behavior-neutral.
  6. `Assets/Scripts/PreviewConfiguration.cs` (+`Assets/Scripts/Preview/PreviewController.cs`) —
     added `bool EmoteLoop` (default **false**) and passed it to `LoadForProfile` in the
     Profile/Authentication case (`LoadForProfile(config.Profile, config.Emote, config.EmoteLoop)`).
     Lets the studio hold a single-frame pose on a profile avatar (§17). Prod default false =
     unchanged; only the Outfit Studio sets it true.

## 3. File map

```
Assets/OutfitStudio/
├── README.md                      # artist-facing workflow doc
├── IMPLEMENTATION.md              # this file
├── Runtime/
│   ├── CatalogModels.cs           # CatalogQuery, CatalogPage, CatalogItem DTOs
│   ├── CatalogService.cs          # GET /v2/catalog (marketplace browse + URN lookup)
│   ├── OutfitDefinition.cs        # the outfit model + share-code round-trip
│   ├── OutfitPreset.cs            # ScriptableObject preset asset
│   ├── TurntableDriver.cs         # deterministic 360° spin MonoBehaviour
│   └── MatcapPresets.cs           # LOCAL COPY of the package type — delete at integration (§16)
├── Shaders/
│   ├── Matcaps/                   # 6 matcap PNGs + MatcapPresets.asset (local copies, §16)
│   ├── DCL_Toon_Studio/           # unlocked copy of the metallic-branch DCL_Toon (§16)
│   ├── DCL_Stylized_PBR/          # new Disney-principled stylized PBR shader (§16)
│   └── StudioCardFrame/           # unlit shader for the Fortnite-style card frame (§18)
├── Textures/
│   └── DclBackgroundPattern.png   # icon-pattern overlay, ported from Explorer's loading screen (§18)
├── CardPresets/                   # CardColorPreset assets (card paint skins, §18)
└── Editor/
    ├── OutfitCapture.cs           # stills + video, both via Unity Recorder (§8)
    ├── EditModeAvatarPreview.cs   # edit-mode outfit assembly on the scene skeleton (§11)
    ├── StudioAvatarShaderSwitcher.cs # 3-way shader enforcement + matcap bootstrap (§16)
    ├── StudioCardFrame.cs         # camera-parented card-frame quads (card/fade/mask/border) (§18)
    ├── StudioGameViewSize.cs      # pins the Game view to the capture's pixel size (§20)
    ├── CardColorPreset.cs         # ScriptableObject: the card's 3 colours + pattern (§18)
    └── OutfitStudioWindow.cs      # the EditorWindow (all UI + orchestration)
```
(Plus `Editor/StudioSceneOverlayHider.cs`, `Editor/StudioRenderPipelineSwitcher.cs`,
`Editor/StudioFlyCameraController.cs` (§12a), `Editor/StudioShaderPreset.cs`,
`Editor/BuilderIdentity.cs`, `Editor/BuilderCollectionService.cs`, `Editor/Plugins/` (vendored
DLLs), `Scenes/OutfitStudio.unity`, `Settings/URP_Asset_Studio.asset` — see §13/§14.)

## 4. How it drives the renderer (the key idea)

The renderer's **Builder mode** already does outfit assembly. The tool just writes into the
existing config singleton and reloads:

```csharp
var config = PreviewConfiguration.Instance;       // Assets/Scripts/PreviewConfiguration.cs
config.SetMode("builder");
config.BodyShape = outfit.bodyShape;           // body shape URN
config.Urns = <wearable URNs>;                 // one per slot; sanitized via URNUtils.SanitizeURN
config.SetSkinColor/SetHairColor/SetEyeColor(hexNoHash);
config.Emote = <embedded name | emote URN>;
FindFirstObjectByType<PreviewController>(FindObjectsInactive.Include).InvokeReload();
```

Reload path: `PreviewController.Reload()` → `LoadForBuilder()` → slot-dedup (one wearable per
category, last wins) → `AvatarLoader.LoadAvatar(...)`. `AvatarLoader` **diffs against
`_loadedModels`**, so swapping one wearable only reloads that wearable — this is what makes
live click-to-equip feel fast. `InvokeReload()` during an in-flight load safely queues
(`_shouldReload` loop), so debounced rapid clicking is safe.

**Play-mode entry:** the window never edits `Bootstrap.debugUrl` (would dirty the scene).
Instead: `applyOnPlay = true` → `EditorApplication.EnterPlaymode()` → on
`EnteredPlayMode`, apply is scheduled ~1s later (lets `Bootstrap.Start()` parse debugUrl and kick
its initial load; our reload then supersedes it).

## 5. Marketplace catalog access

`CatalogService` (Runtime, but editor-safe) hits:

```
GET https://marketplace-api.decentraland.{org|zone}/v2/catalog
    ?first=24&skip=N
    [&category=wearable|emote] [&search=text]
    [&wearableCategory=slot] [&emoteCategory=cat]
    [&rarity=r] [&wearableGender|emoteGender=g] [&sortBy=newest|name|cheapest]
    [&isOnSale=true]          # only ever sent as true — see the On Sale note below
    [&urn=...&urn=...]        # direct URN lookup mode (ignores browse filters)
```

- Environment comes from `Services.APIService.Environment` (`"org"`/`"zone"`) — same switch the
  whole renderer uses; the toolbar prod/dev popup sets it.
- **Callback-based, not `Awaitable`** — deliberately, so it runs in *edit mode* (the browser works
  without entering play). Uses `UnityWebRequest` + `operation.completed`.
- Response parsed with `JsonUtility` into `CatalogPage { CatalogItem[] data; int total; }`.
  `CatalogItem` declares **only consumed fields** (`id, name, thumbnail, urn, category, rarity,
  isOnSale, listings, price, minPrice, createdAt, updatedAt, soldAt,
  data.wearable.{bodyShapes,category,isSmart}, data.emote.{...}`) — extra JSON fields
  are ignored, which keeps us resilient to API additions. Note the timestamps are JSON **numbers**
  on `/v2/catalog` (they were strings on the old `/v1/items`), hence `long` fields, `0` meaning
  absent. `bodyShapes` values are
  `"BaseMale"`/`"BaseFemale"`.
- `CatalogItem.Slot` → wearable category, or `"emote"` for emotes.
- The URN-lookup mode (`CatalogQuery.Urns`) is used to **hydrate** names/thumbnails for URNs the
  window doesn't know (pasted share codes, loaded presets, after domain reload). Off-chain URNs
  (`:off-chain:` base-avatars) are skipped — they're not marketplace items.

Thumbnails: static `Dictionary<string, Texture2D>` cache in the window +
`UnityWebRequestTexture`, textures marked `HideAndDontSave`.

### 5a. Tag-aware search augmentation (2026-07-27)

marketplace-api's `search` param only matches `name`/`description` — it has **no `tags` field at
all** (verified against the live API and its docs). This meant a query like "jacket" would miss an
item named e.g. "Black Jacket" that's tagged "Jacket" but doesn't literally have that word in a
matching position, or any item whose name doesn't contain the query word at all despite being
tagged with it — a real gap versus the web marketplace/game client, which do search tags.

`CatalystTextSearchService` (new, `Runtime/CatalystTextSearchService.cs`) hits the catalyst
content-server's lambdas collections endpoint instead, which *does* index tags:

```
GET https://peer.decentraland.{org|zone}/lambdas/collections/wearables|emotes
    ?textSearch=text&limit=N[&lastId=<urn of last item from previous page>]
```

Cursor-paginated (`response.pagination.next` present ⇒ more pages; `lastId` = the previous page's
last item's `id`, which is its URN). Parsed with `Newtonsoft.Json.Linq` (`JObject`/`JArray`), same
convention as `BuilderCollectionService.cs`, since the payload is nested/inconsistent (`i18n[]`,
`data.tags[]`, `data.representations[]`) rather than the flat shape `JsonUtility` needs. Returns
only matched URNs — no commerce data (price/rarity/on-sale), so it's a discovery-only pass.

`OutfitStudioWindow.RunSearch` (only when `_query.Search` is non-empty):
1. Runs the normal marketplace-api name/description search unchanged (`CatalogService.SearchAll`).
2. `AugmentWithTagMatches` runs `CatalystTextSearchService.SearchUrns` (capped at
   `TAG_SEARCH_CAP=500`) and diffs its matched URNs against what the name search already found.
3. `HydrateTagMatches` hydrates just the *extra* tag-only URNs via the existing
   `CatalogQuery.Urns` direct-lookup path (chunked at `TAG_HYDRATE_CHUNK=50` items per request to
   keep URLs short), then `MatchesActiveFilters` re-applies the current slot/rarity/gender filters
   to that small extra set client-side — a direct URN lookup bypasses those filters server-side
   (see the `[&urn=...]` note above), so this is needed to keep results consistent with what's
   selected in the Slot/Rarity/Body popups. Gender is approximated from `bodyShapes` (verified
   empirically against the live API: "male"/"female" match items serving that shape at all,
   "unisex" requires both).

Net effect: results = old name/description matches ∪ tag-matched items the old code could never
surface, with sort/slot/rarity/gender filters still fully respected. Separate from (and may
partially help) the 2026-07-22 "bare brand-word returns 0" quirk noted in §10 — that one is a
marketplace-api fuzzy-match oddity on the name search itself, still unfixed; this is an additive
tag-search layer on top.

**Editor-verified bug round + fix (same day):** Mauricio repro'd with a real item — "Cyber Xmas
Helmet" (`urn:decentraland:ethereum:collections-v1:xmas_2019:m_cyber_xmas_helmet`) never appeared
searching "xmas helmet" even after the fix above. Diagnosed via a temp `Debug.Log` in
`AugmentWithTagMatches`: the lambdas tag search *did* find both body-shape variants correctly (2 tag
matches), but they still never made it into the grid. Root cause: the original design hydrated
tag-matched URNs through marketplace-api's `CatalogQuery.Urns` direct lookup (`?urn=...`) - verified
via curl that **this lookup only resolves collections-v2 (Polygon) URNs and silently returns
`{"data":[],"total":0}` (HTTP 200, no error) for legacy collections-v1 (Ethereum) items**, even
though those items are completely valid and do show up via marketplace-api's own name search. Tested
against a second unrelated collections-v1 URN (Barbarian Helmet) to confirm it's a general gap, not
item-specific. This silently dropped every hydration for older L1 collections - exactly the kind of
item a tag search tends to surface (newer items usually have decent names already).

**Fix:** rewrote `CatalystTextSearchService.SearchUrns` → `SearchItems`, which builds `CatalogItem`s
directly from the lambdas payload (`i18n[0].text` for name, `thumbnail`, `rarity`, `data.category`/
`emoteDataADR74.category` for slot, `representations[].bodyShapes` reduced to `"BaseMale"`/
`"BaseFemale"` markers) instead of bouncing back through marketplace-api at all. This sidesteps the
v1-URN gap entirely and drops a network round-trip. `OutfitStudioWindow.AugmentWithTagMatches` no
longer needs `HydrateTagMatches`/`ChunkUrns`/`TAG_HYDRATE_CHUNK` (removed) - it merges tag matches
straight in, filtered through the client-side `MatchesActiveFilters` (unchanged). Trade-off:
lambdas-only items don't carry price/on-sale/exact listing dates, so they sort last under
price/date-based sorts - acceptable since this is a supplementary path, not the primary browse.
NOT yet re-verified in the editor after this second fix.

## 6. Renderer change: emote URNs as poses

`PreviewController.LoadForBuilder` previously supported only embedded emotes
(StreamingAssets GLBs) or base64. Added:

```csharp
private static async Awaitable<EntityDefinition> ResolveBuilderEmote(string emoteName)
// null for "idle"; EntityService.GetEntities() for "urn:..." (must be EntityType.Emote);
// EntityDefinition.FromEmbeddedEmote(name, loop: true) otherwise
```

So `config.Emote` can now be any marketplace emote URN → artists pick poses from the
**Emotes / Poses** browser tab. Embedded set remains: `idle, clap, dab, dance, fashion,
fashion-2..4, love, money, fist-pump, head-explode`.

Pose freezing: play-mode transport buttons call `PreviewController.PlayEmote/PauseEmote/
StopEmote/GoToEmote(seconds)`; the scrub slider's `highValue` is polled from
`GetEmoteLength()` every 500 ms.

## 7. Share code & presets (outfit reproducibility)

`OutfitDefinition` = `{ bodyShape URN, urns[], skinColor, hairColor, eyeColor, emote }`.

- `ToShareCode()` emits `?mode=builder&bodyShape=...&urn=...&urn=...&skinColor=RRGGBB&...&emote=...`
  — **intentionally identical to `PreviewConfiguration`'s builder query-string format**, so a share
  code works as `Bootstrap.debugUrl`, as URL params on the deployed web renderer, and back into
  the tool. Do not diverge from that format.
- `FromShareCode()` is a tolerant parser (accepts full URLs, ignores unknown params) — kept
  separate from `PreviewConfiguration.RecreateFrom` because that mutates the global singleton and
  `APIService.Environment`.
- `OutfitPreset` (ScriptableObject, `CreateAssetMenu` "Decentraland/Outfit Preset") wraps an
  `OutfitDefinition` for named local presets; window has Load / Save / Save As via
  `AssetDatabase`.

## 8. Capture

`OutfitCapture` (static, editor-only):

**Still** (2026-07-27 rewrite) — captures via the Unity Recorder package's `CameraInputSettings`
("Targeted Camera" input, `Source = MainCamera`), single-frame mode
(`RecorderControllerSettings.SetRecordModeToSingleFrame(0)`), same as **Video** below just with an
`ImageRecorderSettings`/PNG recorder instead of a movie one. **Async**: `CaptureStill(..., Action<string>
onComplete)` starts the Recorder and polls `EditorApplication.update` until `IsRecording()` goes
false, then locates the newly-written file (`Directory.GetFiles` before/after diff — the Recorder
always suffixes a frame index, so the exact filename isn't predictable up front) and invokes the
callback. `IsCapturingStill` guards re-entry the same way `IsRecording` guards video.

**Why not a direct render (the pre-2026-07-27 approach):** the original still path rendered
`Camera.main` into an arbitrary-resolution `RenderTexture` via `RenderPipeline.StandardRequest` +
`SubmitRenderRequest`, then `ReadPixels` → `EncodeToPNG`. Geometry/lighting matched the Game view,
but **Bloom and any additive-blended VFX (rarity sparkle particles) came out dimmed or missing
entirely** — confirmed by the fact that Video (which was already Recorder/Game-View-based) captured
bloom correctly while the manual-RenderTexture stills didn't, even with an opaque background. The
`SubmitRenderRequest` path doesn't go through the same per-camera "after render" capture hook
(`CameraCapture.AddCaptureAction`) that the interactive render and the Recorder's own `CameraInput`
use — root-caused by testing, not by reading URP source (a `msaaSamples` mismatch against the
project's MSAA-disabled URP assets was suspected and fixed first, and did fix a real but smaller
color-desaturation issue on thin bright details, but didn't fix the missing-bloom regression — that
needed the full switch to Recorder/`CameraInputSettings`). Superseded entirely; `RenderTexture`/
`RenderPipeline` are no longer used anywhere in `OutfitCapture`.

Transparent background (`transparentBackground` toggle, or the Card Frame simply being enabled — it
has no background layer at all as of 2026-07-30, see §18) = temporarily set the camera to solid-color
clear with alpha 0, plus
`CameraInputSettings.RecordTransparency` / `ImageRecorderSettings.CaptureAlpha` so the Recorder's
copy shader preserves the channel through to the PNG.

**`RecoverAdditiveAlpha`** (post-pass on the saved PNG, only when capturing transparent): Bloom and
additive-blended VFX write RGB straight into the color buffer via `Blend One One`, never touching
alpha. Over the old opaque background that was invisible (final alpha was 1 everywhere regardless),
but with a transparent clear those glow pixels keep alpha 0 from the clear and vanish once exported
to PNG even though their color is genuinely there. Fully un-premultiplying (scaling every touched
pixel so its brightest channel hits 255, matching a brightness-derived alpha) is "physically
correct" but also fully saturates the dim outer fringe of the bloom blur — often a blend of two or
more nearby sparkles' colors mixing (e.g. green+yellow bleeding into a muddy olive) that was
invisible before purely because its alpha was near 0; maxing it out makes the mixed hue loudly
visible instead of subtle. Fix: curve the recovered alpha by `brightness²` so faint/mixed fringe
pixels stay suppressed toward transparent (same as they'd fade to black in the editor) while
genuinely bright sparkle cores stay vivid and correctly colored — a **stylized approximation**, not
a physically-exact one (additive light fundamentally can't look identical over both black and white
backgrounds; this trades strict correctness for not exposing color-mixing artifacts). Since the
Recorder writes straight to disk, this runs by reading the saved file back in (`Texture2D.LoadImage`),
patching, and re-encoding over it — there's no in-memory `Texture2D` to intercept mid-pipeline
anymore.

Runtime UI Toolkit overlays don't render through the camera, so stills are automatically clean.

**Video** — Unity Recorder driven programmatically: `RecorderControllerSettings` +
`MovieRecorderSettings` with `CoreEncoderSettings { Codec = MP4, Quality = High }` and
`GameViewInputSettings { OutputWidth/Height }`, manual record mode, `FrameRate` capped.
Caveat: Game view capture **includes runtime UI overlays** (builder-mode zoom buttons etc.).

Three video flows in the window:
- Manual **Start/Stop**.
- **Record Emote**: `pc.PlayEmote()` + auto-stop scheduled at `GetEmoteLength() + 0.5s`.
- **Record Turntable**: `TurntableDriver` added to the avatar's `DragRotator` GameObject —
  disables the `DragRotator` while active (they'd fight over the transform), rotates exactly 360°
  over `Duration`, restores rotation, fires `Completed` → stop recording. Re-enable pattern:
  `driver.enabled = false; configure; subscribe; StartVideo; driver.enabled = true`
  (OnEnable resets its state).

Output: `Captures/` **next to the project root** by default (outside `Assets/` so Unity doesn't
import the files); absolute paths allowed. Filenames `outfit_yyyyMMdd_HHmmss`.

**Pose controls (2026-07-24, added to the Capture pane):**
- **Rotation snap** (`<`/`>` buttons, ±15° per click, `rotationSnapAngle` accumulates and persists):
  `DragRotator.SnapRotation(yawDegrees)` smoothly rotates to an absolute yaw relative to the spawn
  orientation (`ResetRotation`'s target), resetting drag velocity/timing state so free-drag orbiting
  still works normally once it settles. Play mode only (`EnsurePlaying`).
- **Look at Camera** button: turns the head bone toward `Camera.main`, giving the neck bone a
  `NECK_LOOK_SHARE` (0.4) share of the turn so it doesn't read as the head twisting on its own —
  `AvatarLoader.HeadBone`/`NeckBone` resolve `"Avatar_Head"`/`"Avatar_Neck"` by exact glTF node name
  first, falling back to a `"...Head"`/`"...Neck"` suffix match. **One-shot**: calls
  `AvatarLoader.FreezePose()` (`avatarAnimation.Stop()`) first, since the legacy `Animation`
  component would otherwise re-sample the head/neck rotation from the pose/emote clip on the very
  next frame and undo the adjustment immediately — so this is a pose you apply right before
  capturing, not a persistent look-at behavior. `AvatarLoader.MainCamera` was added as a public
  accessor for this (previously private).

## 9. Window internals worth knowing

(`OutfitStudioWindow.cs`)

- **State survival**: outfit + capture settings are `[SerializeField]` fields → survive domain
  reload / play transitions. Browser results and `_knownItems` (urn → CatalogItem) are session
  state, re-hydrated via §5 URN lookup in `CreateGUI`/`LoadOutfit`.
- **Slot semantics**: `outfit.urns` is a flat URN list (matches the renderer). The window enforces
  one-per-slot at click time by removing any URN whose *known* item shares the clicked item's
  `Slot`. Unknown URNs can't be checked — the renderer's own slot-dedup (last-in-list wins) is the
  backstop, and picks are appended so they win.
- **Body-shape guard**: `FilterForBodyShape()` drops known wearables lacking a representation for
  the selected shape at apply time, with a status warning. Without this,
  `GLTFLoader.LoadModel` → `EntityDefinition[bodyShape]` **throws** and breaks the whole load.
  Unknown URNs pass through unchecked.
- **Auto-apply**: debounced 400 ms via `IVisualElementScheduledItem` (`_pendingApply.Pause()` +
  reschedule). Only fires in play mode with the toolbar toggle on.
- **Search**: debounced 500 ms; out-of-order responses discarded via `_searchSequence` counter.
- **Safety hooks**: `ExitingPlayMode` → `OutfitCapture.StopVideo()`.

## 10. Known caveats / future work

- **Recorder version**: `com.unity.recorder 5.1.2` — if Unity can't resolve it, bump/adjust in
  `Packages/manifest.json`; the API used (`EncoderSettings`, `CoreEncoderSettings`,
  `GameViewInputSettings`) is Recorder 4.0+.
- Game-view video includes runtime UI overlays; a "hide UI during recording" toggle (disable
  `UIDocument` components temporarily) is an easy v2.
- `/v2/catalog` response fields were implemented from the documented schema; if the grid comes up
  empty with a successful request, diff the live JSON against `CatalogModels.cs` first.
- Docked 3D preview inside the window (RenderTexture) — deferred; the Game view is the viewport.
- Possible v2s: load-outfit-from-profile (via `APIService.GetAvatar`), multi-rarity filters,
  transparent-background video (WebM), smart-wearable filtering, preset thumbnails.
- The base-avatar (off-chain) wearables can't be browsed — the marketplace API only serves
  collection items. Artists get default body parts unless they equip marketplace items; browsing
  base-avatars would need the catalyst entities endpoint instead.
- **Catalog search quirks (2026-07-22 investigation, not a client bug):** hitting the live
  `marketplace-api.decentraland.org/v1/items` directly (outside Unity, same params `CatalogService`
  sends) confirms two upstream behaviors, not bugs in this repo:
  1. ~~Some single-word searches return 0 even though the item exists and is indexed — e.g.
     `search=atari` → 0, but `search=Orange Atari` → 4~~ — **`/v1/items` only; fixed 2026-07-30 by
     moving to `/v2/catalog`** (§"On Sale" note below). Same query, same params: `/v1/items?
     search=atari` → 0, `/v2/catalog?search=atari` → 26 (Green/Blue/Purple Atari Tee, Atari Cap, …).
     No two-word workaround needed any more.
  2. **`sortBy` has no effect at all** — tried `newest`/`name`/`cheapest`/`recently_listed`/
     `recently_sold`/`most_expensive`/`issued_id_asc`/`issued_id_desc`, the alternate param name
     `orderBy`, combined with `sortDirection`/`isOnSale=true`, and cache-busting query params:
     every combination returned items in the exact same order (confirmed via `createdAt`/`price`
     not being monotonic even for "newest"/"cheapest"). The endpoint also never errors on an
     invalid enum value for `category`/`sortBy` — it silently falls back to a default — so there's
     no way to discover a "correct" value/param name by trial and error; the sort feature appears
     genuinely unsupported by this endpoint version.

     **Fixed client-side, twice.** First pass: `CatalogItem` gained `price`/`createdAt`, and
     `SortForDisplay` re-sorted just the already-fetched page — a per-page sort only, not a true
     catalog-wide one. **2026-07-23 upgrade**: `CatalogService.SearchAll(query, cap, onSuccess,
     onError)` ignores the query's `Skip`/`First` and instead pages through `Search` itself (at up
     to `MAX_FETCH_PAGE=1000` items/request) until it has every item matching the current filters, a
     server-reported total, or `cap` items — whichever comes first. `OutfitStudioWindow.RunSearch`
     calls it with `FETCH_CAP=3000`, then sorts the **entire fetched set** client-side
     (`SortForDisplay`) and paginates (`PAGE_SIZE=36`) locally over that in-memory array — so
     Next/Prev and re-sorting no longer re-query the server at all. `CatalogItem` also gained
     `updatedAt`/`soldAt` to support "Recently Listed"/"Recently Sold" (`OrderByTimestamp` helper;
     items missing the relevant field always trail last). Sort option labels/order now match the
     real marketplace dropdown exactly (Newest/Recently Listed/Recently Sold/Cheapest/Most
     Expensive/Name — "Name" is local-only, not a marketplace concept). A new **↓/↑ invert button**
     next to the Sort dropdown flips direction (e.g. Newest+invert = oldest first) — the only way to
     reach the tail of a sort, since the marketplace's own dropdown has no "oldest" equivalent. Still
     **not a true global sort** for a broad unfiltered browse (~11k wearables > `FETCH_CAP`) — the
     status label reads "N of total items (sort limited to the first 3000)" whenever the cap is hit,
     rather than silently truncating.

     (Re-verified on `/v2/catalog` 2026-07-30: `sortBy` is *still* not honored — `newest` isn't
     monotonic in `createdAt`, `most_expensive` isn't monotonic in `minPrice` — so the client-side
     sort stays.)
- **"On Sale" toggle → `/v2/catalog` (2026-07-30):** the toggle used to be filtered **client-side**
  against each item's own `isOnSale` field, which was wrong twice over, and the endpoint the tool
  browsed (`/v1/items`) was the root cause:
  1. `/v1/items` serves **stale sale data**. For "Donald Dump": `isOnSale=false`, `price="0"` — while
     `/v2/catalog` reports the same item mintable at 30 MANA with an open 50 MANA listing, and
     `/v1/orders` confirms the live listing. That's why searching "donald" only found it with the
     toggle **off**, the bug this replaced.
  2. Even with fresh data, the `isOnSale` **field** ≠ the marketplace's "on sale". The field means
     *mintable from the collection store*; the marketplace also counts items with open secondary
     listings. A sold-out item with 1000+ open listings (e.g. "Metaverse Art Week Headset",
     `listings=1045`) reports `isOnSale=false` yet is plainly on sale — 2671 such wearables passed
     `isOnSale=true&onlyListing=true` at the time of writing, i.e. the old client-side filter hid
     thousands of buyable items.

  Now sent **server-side** as `isOnSale=true` (`CatalogService.BuildUrl`), which is exactly what the
  web marketplace does (verified in the marketplace-site bundle: `onlyOnSale && append("isOnSale")`
  against `/v{1,2}/catalog`), so the semantics match by construction. Two gotchas encoded in code:
  - **Never send `isOnSale=false`.** It is not the neutral value — the endpoint reads it as "only
    items that are *not* on sale" (`search=donald&isOnSale=false` → just the one unlisted hit). An
    off toggle omits the param entirely, which is what makes "off = show everything" true.
  - **URN-lookup mode ignores it** (like the other browse filters), so hydration is unaffected.

  `CatalogItem.IsBuyable` (`isOnSale || listings > 0`) mirrors the server-side predicate for the two
  places that still need it locally: price sorting (`OrderByPrice`, now on `minPrice` — the cheapest
  real way to acquire the item, matching the marketplace's own cheapest/most-expensive) and the
  lambdas tag-match extras in `MatchesActiveFilters`. Those extras carry no price/listing data at
  all, so with the toggle on they drop out and results narrow to what marketplace-api filtered.

## 11. Edit-mode 3D preview (iteration 2)

Outfit selection previews **without play mode**: `EditModeAvatarPreview.Apply(outfit, status)`
assembles the outfit onto the Preview rig's scene skeleton in edit mode. The window routes
`Apply()` here whenever `!Application.isPlaying`; the play-mode path is unchanged. Play mode is
still required for emote playback and capture.

**Why it works** (verified in code): `AvatarUtils` is pure component logic; the load path
(`GLTFLoader`/`BinaryDownloadProvider`) only awaits `UnityWebRequest` + `Task.Yield()` (pumps on
the editor sync context, no frame waits); glTFast's `IsEditorImport` gate is irrelevant for
GLB-embedded textures (`CreateTexturesFromBuffers` ignores it).

**Edit-mode blockers and their fixes:**
1. `CommonAssets.AvatarMaterial/FacialFeaturesMaterial` normally set in `Bootstrap.Start` →
   `EnsureEditModeSetup()` reads Bootstrap's serialized `baseMat`/`facialFeaturesMat` via
   `SerializedObject`.
2. glTFast's lazy default defer agent doesn't tick in edit mode →
   `GltfImport.SetDefaultDeferAgent(new UninterruptedDeferAgent())` before first load.
3. `GLTFLoader.Sanitize` used `Object.Destroy` (illegal in edit mode) → patched to
   `DestroyImmediate` when `!Application.isPlaying` (play mode/builds byte-identical).
4. `AvatarLoader` is play-mode-only (`Destroy` in its reload diff) → NOT reused. The preview
   keeps its own `urn → LoadedModel` dicts and destroys with `DestroyImmediate`. Assembly mirrors
   `AvatarLoader.LoadAvatar` post-load steps (AvatarLoader.cs:149-169): slot dedup +
   `HasRepresentation` skip → `HideWearables` → load body+wearables (`GLTFLoader.LoadModel`) and
   facial features → `HideBodyShape` → `SetupFacialFeatures` (the defaults dict self-populates on
   first call) → per-model `SetupWearable(go, colors, _, avatarRootBone, avatarBones)`.

**Scene skeleton access:** private serialized fields are read via `SerializedObject` —
`PreviewController.avatarLoader` → `AvatarLoader.avatarRootBone/avatarBones/avatarAnimation`.
No renderer API changes needed.

**Lifecycle & safety:**
- The Preview rig is saved **inactive** in `Main.unity` (Bootstrap activates it at play time) —
  `EnsureActiveInHierarchy` activates the rig/skeleton ancestor chains for the preview and
  `Clear()` restores the original inactive state (so Bootstrap keeps owning play-time state).
- Preview roots get `HideFlags.DontSave` recursively — never saved into the scene.
- All preview objects live under a `__OutfitStudio_EditPreview` container. The tracking dicts are
  static and die on **domain reload** while the objects survive in the scene; the container makes
  those orphans discoverable — `Apply` sweeps it when the dicts are empty, and `Clear()` does a
  name-based sweep across all `AvatarLoader`s. Without this, applies after a recompile stack
  duplicate meshes. `[InitializeOnLoad]` re-registers the cleanup hooks on every reload.
- `Clear()` runs automatically on `ExitingEditMode` (runtime loader always starts clean) and on
  scene closing; also exposed as the toolbar **Clear Preview** button. A body-shape change
  mid-apply calls `Clear()` then re-arms its own sequence + re-activates the rig.
- Overlapping async applies are serialized with a sequence counter (stale loads are disposed).
- Pose: samples the skeleton's "Idle" clip at t=0 (`SampleIdlePose`) so the avatar isn't in bind
  pose. This moves scene bones and may mark the scene dirty — harmless.

**Limitations:** static idle pose; no spring bones, outline, or emotes in edit mode; glTFast caps
skin weights to 4 outside play mode (minor deformation differences possible). Play mode is ground
truth for capture.

## Status as of 2026-07-15

**New (iteration 5, untested in-editor):** the 3-way shader switcher (§16) — Shader section in
the outfit pane, `DCL_Toon_Studio` (metallic-branch copy + rim/lights/GI unlocks),
`DCL_Stylized_PBR` (new Disney-principled stylized PBR), `StudioAvatarShaderSwitcher`
enforcement, and the verbatim `ToonMaterialGenerator`/`CommonAssets` metallic port + local
`MatcapPresets`. First editor focus needs to compile both shaders — run §16's verification
list. Everything from the 2026-07-14 status below still applies.

## Status as of 2026-07-14 (end of day)

**Verified working by Mauricio on 2026-07-14:**
- Edit-mode 3D preview (after the inactive-rig + orphan-sweep fixes), thumbnail pagination fix,
  Debug tab + Clean View (§12).
- **Load from Collection (§13) — confirmed working end-to-end**, including the signed
  builder-api access (identity paste + auth-chain signing) and equipping draft items via base64.

**Not yet tested:** capture paths (Still / Video / Record Emote / Turntable — Recorder 5.1.2
installed, code unexercised), presets save/load, share-code load round-trip, body-shape
switching, `dev (zone)` env, emote-URN poses from the Emotes/Poses tab.

**In progress:** dedicated studio scene (§14) — raw copy + menu shortcut + overlay hider +
studio render pipeline (per-pixel additional lights for set geometry) all in place; Mauricio is
mid set-dressing (own CinemachineCamera added; stock vcams to disable, Configurator to strip).
Target look = Fortnite-style item cards; see §14 for the lighting findings (avatar = 1
directional only, rim compiled off) and the planned shared-dependencies shader session
(rim promotion + optional additional lights), to be bundled with the metallic integration.
Untried: the back-key + post-lift interim recipe; the studio renderer-data duplicate for
tweakable gradient background colors (Route A).

## Metallic/normals branch — integration options (deliberately NOT done yet)

The normals + stylized-metallic matcap work lives on `feat/avatar-toon-shader-metallic-normals`
and is intentionally kept OFF this branch. Facts for whenever we want to see it in the Outfit
Studio (verified 2026-07-14):

- The shader HLSL lives in the **`unity-shared-dependencies` package**; the metallic branch
  repoints the package to `#feat/toon-normalmap-stylized-metallic` (NOT merged to the package's
  main) via `Packages/manifest.json` + lock.
- Aang-side changes committed on that branch (diff vs main): `Assets/Scripts/Loading/
  ToonMaterialGenerator.cs` (+123 — maps GLB normal/metallic data + matcaps into DCL_Toon),
  `Bootstrap.cs` + `CommonAssets.cs` (+9 each — matcap preset wiring), `Assets/Scenes/Main.unity`
  (serialized refs), `Assets/StreamingAssets/character/PuffyJacket.glb` (test asset).
- The `git stash` ("WIP metallic/normals test harness") holds only the optional
  `LocalWearableOverride` local-GLB harness — NOT required to see the effect.
- **Repointing the package alone is not enough** — the `ToonMaterialGenerator` changes are what
  feed the new shader features.

**Update 2026-07-15 (iteration 5, §16):** the `ToonMaterialGenerator` + `CommonAssets` diffs
have now been ported onto THIS branch verbatim (prod-safe — the stock package shader ignores
the extra properties), and the metallic shader itself was copied locally into
`DCL/DCL_Toon_Studio`. Remaining "integration" with the metallic branch is therefore only the
package repoint (option 1/2 below) plus deleting the local duplicates flagged in §16.

Options, in recommendation order:
1. **Integration branch** — `feat/outfit-studio-metallic` = this branch + `git merge
   feat/avatar-toon-shader-metallic-normals`. Both source branches stay pure for upstream PRs.
   Expect one small conflict in `manifest.json`/`packages-lock.json` (keep BOTH the
   `com.unity.recorder` line and the shared-deps branch repoint). Bonus: metallic draft
   wearables can then be QA'd through Load from Collection.
2. **Merge metallic into `feat/outfit-studio`** — simplest daily workflow, but entangles the
   histories (outfit-studio PR would carry the shader work).
3. **Working-tree quick look (no commits)** — `git checkout
   feat/avatar-toon-shader-metallic-normals -- <the files listed above>`; revert with
   `git restore`. Minutes to see, fragile to keep (don't commit the manifest/lock repoint).

## Status as of 2026-07-13 (commit cd2afbb)

**Verified working (by Mauricio):**
- Catalog browsing (search/filters/pagination, ~429 items), equipping into slots, share-code UI.
- Play-mode flow: outfit loads via builder mode, live re-apply while picking items.

**Fixed late in the session — needs re-verification:**
- Edit-mode preview visibility: the Preview rig is saved inactive in the scene; fixed via
  `EnsureActiveInHierarchy` + restore-on-Clear. Not yet confirmed after the fix.
- Mesh stacking on same-slot swaps: root cause was domain-reload orphans (see §11 lifecycle);
  fixed via the `__OutfitStudio_EditPreview` container + sweeps. **First action after checkout:
  press Clear Preview once (or just Apply) to sweep any orphans left in the open scene.**
- `EntityService` assertion removed — unresolved entities (likely a third-party/linked wearable)
  now warn + skip instead of failing the load. The offending URN was never identified; if it
  reappears, the console lists it (`[OutfitStudio] Could not resolve entities for: ...`).

**Not yet tested at all:**
- Capture: Still / Start-Stop Video / Record Emote / Record Turntable (Recorder package installed,
  code untested — expect possible API/version friction on first run).
- Presets (save/load/save-as), Load from share code, body-shape switching, `dev (zone)` env,
  emote-URN poses via the Emotes/Poses tab, emote scrubbing.

## 12. Debug tab & Clean View (iteration 3)

The renderer auto-shows its built-in debug overlay in editor play mode (`PreviewUIPresenter.
OnEnable` → `EnableDebug()` when `Application.isEditor`; unlocked in builds by typing
`debugmesilly` — that gating is untouched). The window replicates that functionality in a
third **Debug** tab and hides the overlay for a clean, avatar-only Game view:

- **Debug tab** (`BuildDebugPane`): JSBridge method dropdown (same reflection as the overlay:
  `typeof(JSBridge).GetMethods(DeclaredOnly|Public|Instance)`) + Parameter + Invoke with the
  identical auto-Reload rule (skip for `Reload`/`TakeScreenshot`/`Cleanup`); URL presets from
  `PreviewUIPresenter.DEBUG_URL_PRESETS`; Print Config (logs + fills a read-only field);
  Random Profile (`SetProfile("default"+Random(1,160))`); Zoom In/Out via
  `PreviewCameraController.ZoomIn/ZoomOut`. All actions require play mode (status warning
  otherwise) and go through `SendToJSBridge` (`GameObject.Find("JSBridge").SendMessage`).
- **Clean View** (toolbar toggle, default ON): a 500 ms scheduled loop
  (`EnforceCleanGameView`) hides `DebugPanel`, `ZoomControls`, `Switcher`, `EmoteControls`
  on the runtime UIDocument while playing. Re-enforcement is needed because
  `PreviewController.Reload()` re-enables controls after every load (so the overlay may flash
  briefly post-reload). **Important:** the `Controls` element itself must NEVER be hidden —
  it carries the `DragManipulator` for mouse rotation; only its child widgets are hidden.
  The loader spinner stays visible. Toggling Clean View off restores the debug panel
  (editor-only, mirroring the presenter) and triggers a `Reload` so `PreviewController`
  re-applies the mode-dependent control visibility.

### 12a. Fly camera (2026-07-28, studio scene + play mode only)

"Fly Camera" section — in the Debug tab originally, **moved to the outfit pane's "Scene and Camera
settings" foldout on 2026-08-04** (§24), below the lights: hold the right mouse button and use WASD/QE
(Shift = faster) to free-fly the camera, Scene-view style — on top of the existing LMB-drag avatar rotation
and Zoom In/Out (those are unaffected; RMB was otherwise unclaimed). Off by default (`Enable` toggle),
plus Move/Look speed sliders and a "Reset View" button.

**Why it needs to fight Cinemachine:** the studio camera is normally driven every frame by whichever
`CinemachineCamera` vcam is prioritized, through the `CinemachineBrain` on the camera GameObject —
writing `transform.position/rotation` directly would just get overwritten next frame. `StudioFlyCamera`
(`Assets/OutfitStudio/Runtime/StudioFlyCamera.cs`, a plain runtime `MonoBehaviour`) disables the brain
the moment RMB is first pressed, so its own writes stick; it deliberately does **not** re-enable the
brain on RMB release (that would snap the view back to the vcam's authored framing mid-fly, defeating
the point of "fly around and stay there") — `ReleaseToCinemachine()` (wired to the "Reset View" button)
hands framing back explicitly. Yaw/pitch are re-synced from the live transform at the *start* of each
fly session (not just once in `Awake`), so releasing, resetting the view, and flying again doesn't snap
to a stale angle from a previous session.

**Bootstrap (poll-based, like `StudioCardFrame`/`StudioAvatarShaderSwitcher`):**
`Assets/OutfitStudio/Editor/StudioFlyCameraController.cs` ([InitializeOnLoad], EditorPrefs keys
`OutfitStudio.FlyCamera.*`) adds/removes `StudioFlyCamera` on the studio's live camera every 0.5 s
tick, gated on studio-scene + `Application.isPlaying` + the `Enabled` toggle. The 0.5 s cadence is fine
here because the poll only needs to notice on/off and scene/play-mode transitions — the actual
per-frame fly movement runs inside `StudioFlyCamera.Update()`, driven by Unity's normal player loop
(correct ordering against `CinemachineBrain`, no jitter). Camera resolution reuses
`StudioCardFrame.FindCamera()` (promoted from `private` to `internal`) rather than raw `Camera.main`,
since the studio scene can have more than one GameObject tagged `MainCamera` (e.g. before the
Configurator camera is stripped, same reason `StudioCardFrame` needed the fallback search).
Uses the new Input System (`Mouse.current`/`Keyboard.current`) — legacy `Input.*` is disabled
project-wide (`activeInputHandler: 1`).

## 13. Load from Collection (iteration 4)

Debug-tab section mirroring the explorer's `--self-preview-builder-collections` flag: paste a
collection ID → **Load** → paginated grid → click to equip.

**Two ID kinds:**
- **`0x` contract address (published)** — unauthenticated `marketplace-api /v2/catalog?
  contractAddress=...` via `CatalogService` (`CatalogQuery.ContractAddress`), server-paged.
  Tiles reuse the normal URN equip flow.
- **UUID (draft/unpublished)** — `GET builder-api.decentraland.{env}/v1/collections/{id}/items`
  (`BuilderCollectionService`), which **requires a signed auth chain**. Whole collection returned
  at once; client-side paging (24/page).

**Auth (drafts):** `BuilderIdentity` — the user pastes their Decentraland identity JSON from
builder.decentraland.org localStorage (parser is tolerant: finds the
`ephemeralIdentity/expiration/authChain` object anywhere in the pasted JSON, including
stringified nesting). Stored in **EditorPrefs only** (`OutfitStudio.BuilderIdentity`) — the
ephemeral private key must never reach project files or logs. Signing mirrors the explorer
exactly (unity-explorer refs: `WebRequestSignInfo.NewFromRaw`, `WebRequestHeadersInfo.WithSign`,
`RequestEnvelope.SignRequest`, `DecentralandIdentity.Sign`, `NethereumAccount.Sign`):
string-to-sign `"{method}:{path}:{unixMs}:{metadata}"` lowercased (metadata `{}`), personal-sign
with the ephemeral key (`EthereumMessageSigner.EncodeUTF8AndSign`), headers
`x-identity-auth-chain-{i}` (stored chain + appended `ECDSA_SIGNED_ENTITY` link),
`x-identity-timestamp`, `x-identity-metadata`.

**Crypto DLLs:** `Editor/Plugins/` vendors Nethereum.Signer + Hex/RLP/Util/Model +
BouncyCastle + Microsoft.Extensions.Logging.Abstractions, copied from
`unity-explorer/Explorer/Assets/Plugins/Nethereum/net472UnityCommonAOT` (aang's
`apiCompatibilityLevel: 6` = .NET Framework, so the net472 builds match). Their `.meta` files
are hand-written with **editor-only** PluginImporter settings — verify Nethereum never appears
in a WebGL build report.

**Equipping drafts — the renderer's base64 mechanism, zero renderer changes:**
`BuilderCollectionService` converts each draft item into a `RawActiveEntity` JSON
(`Assets/Scripts/Data/RawActiveEntity.cs` shape; representation contents → `{key, url}` against
the public `.../v1/storage/contents/{hash}`; emotes under `emoteDataADR74` with `data` omitted
since `IsEmote` keys off empty `data.category`), base64-encoded into
`OutfitDefinition.base64Items`. Play mode: `Apply()` fills `PreviewConfiguration.Base64` →
`LoadForBuilder` gives base64 per-category priority; a base64 emote overrides the pose.
Edit mode: `EditModeAvatarPreview` parses them via `EntityDefinition.FromBase64` into the slot
dict (emotes skipped — static pose). Share codes carry drafts as `&base64=` params
(`Uri.EscapeDataString`-escaped because `HttpUtility.UrlDecode` would eat `+`); they round-trip
through `Bootstrap.debugUrl` and the web renderer. Draft-vs-catalog slot conflicts are resolved
on equip (both lists purged for the category); picking an embedded/catalog emote removes any
draft emote (which would otherwise take priority).

## 14. Dedicated studio scene

`Assets/OutfitStudio/Scenes/OutfitStudio.unity` is a **copy of Main.unity** (fresh GUID, never
in Build Settings → zero build impact) meant to become the "studio set": custom backdrop,
lighting and post-processing for beauty shots, authored with the normal scene workflow. Open it
via **Decentraland ▸ Open Outfit Studio Scene**. The tool is scene-agnostic (all lookups are
`FindFirstObjectByType`), so it works identically in either scene.

**Stripping rules (as of the copy, still un-stripped):** only the **Configurator branch** (its
rig, second `AvatarLoader`, cameras, UI) is safe to delete. Everything else in the Preview
branch must STAY even if unused, because `PreviewController.Reload()` dereferences it
unconditionally every reload: `wearableLoader`, `confirmationVFX`, `animationReference`,
`platform`, `previewUIPresenter`. Also keep the `UI` GameObject — mouse-drag rotation runs
through its `DragManipulator` (Clean View hides its visuals anyway). Lighting/post are free:
replace the directional light, add a global URP `Volume`, enable post-processing on the camera
directly in the scene.

**Overlay in the studio scene:** `StudioSceneOverlayHider` ([InitializeOnLoad], cadence like
Clean View) force-hides `DebugPanel/ZoomControls/Switcher/EmoteControls/Loader` whenever the
studio scene is active — edit and play mode, window open or not. The `UI` GameObject must stay
alive (PreviewController dereferences the presenter unconditionally; drag rotation runs through
the panel).

**Cameras in the studio scene:** keep the real `Main Camera` (with `CinemachineBrain`) and the
`PreviewCameraController` component — `PreviewController.Reload()` calls `SetMode()`
unconditionally, and it `Prioritize()`s its serialized vcams every reload. To use a custom
CinemachineCamera: **disable (don't delete) the stock vcam GameObjects** (authProfile/
marketplaceWearable/marketplaceAvatar/builder/jesus) — disabled vcams don't compete in the
brain, so the custom camera wins regardless of priority, and `Prioritize()` on a disabled vcam
is a harmless field write. Deleting them NREs `SetMode`.

**Studio lighting — IMPORTANT limitation:** the avatar's shader (`DCL/DCL_Toon`) is lit by
**exactly one light — the main directional**. Its additional-lights loop is **commented out**
in the fragment code (`DCL_ToonBodyDoubleShadeWithFeather.hlsl:~304`, package
`unity-shared-dependencies`; `CalculateAdditionalLightingColour` in `DCL_ToonLighting.hlsl` is
dead code). Point/spot lights do NOT affect the avatar regardless of URP settings. Ambient/GI
is also compiled out (`#define _GI_Intensity 0.0f` in `DCL_ToonVariables.hlsl`); ambient only
leaks via the SH fallback light color when no main light is present.

`Assets/OutfitStudio/Settings/URP_Asset_Studio.asset` (Additional Lights **Per Pixel / limit 8**,
same renderer data + volume profile by GUID) + `StudioRenderPipelineSwitcher`
([InitializeOnLoad], overrides `QualitySettings.renderPipeline` while the studio scene is
active, restores on leave) therefore benefit the **set geometry/props only** (standard URP
shaders take per-pixel spots/points + shadows). Caveat: saving the project *while in the studio
scene* diffs `ProjectSettings/QualitySettings.asset`; self-reverts after leaving + saving —
don't commit.

Avatar light-rig reality: 1 directional (color/intensity/angle = the cel bands), set/post for
mood. The shader's built-in rim light is compiled to near-invisible
(`DCL_ToonVariables.hlsl`: `_RimLight 1.0` but `_RimLight_Power 0.3`,
`_Tweak_RimLightMaskLevel -0.9` — compile-time constants, not tweakable from materials).

**Goal look (Fortnite-style item cards):** gradient background + glow (the renderer's
`BackgroundRendererFeature` ships exactly this — for studio-only tweakable colors, duplicate
the renderer data asset and point `URP_Asset_Studio.m_RendererDataList` at the copy), bloom on
emissives, and a strong cool **top-back rim light** — which is NOT currently reproducible as
light (no additional lights, rim compiled off). Interim technique that works today: use the
single directional AS the back/rim key (top-behind, cool tint → the toon lit band becomes the
rim) and lift the front with post (Shadows/Lift, warm) — see the 2-Ball reference; its front is
mid-dark too.

**Shader session — DONE LOCALLY (2026-07-15, iteration 5, see §16):** all three unlocks (rim
promotion, additional-lights loop, `_GI_Intensity`) now exist in the local
`DCL/DCL_Toon_Studio` shader copy under `Assets/OutfitStudio/Shaders/`. A future
`unity-shared-dependencies` session is now only about **upstreaming** that promotion diff into
the package (the studio copy is the reference implementation).

**Sync ritual:** this copy does NOT receive upstream `Main.unity` changes. After merging main
into the branch, if avatar loading/rig behavior changed: `git diff <old>..<new> --
Assets/Scenes/Main.unity` and re-apply relevant changes (or re-copy Main and re-strip/re-dress).
Renderer *script* changes flow automatically — only scene-serialized wiring drifts.

## 15. Verification checklist (first run after checkout)

1. Focus Unity (project open on `feat/outfit-studio`) → Recorder package installs, scripts
   compile, `.meta` files generate for `Assets/OutfitStudio/`.
2. **Decentraland ▸ Outfit Studio** → search "jacket", slot filter `upper_body` → grid populates.
3. Equip items → **▶ Enter Play** → avatar assembles in Game view; further clicks hot-swap.
4. Capture Still (transparent on/off), Record Emote, Record Turntable → files in `Captures/`.
5. Copy share code → Load from code → identical avatar. Same string pasted into
   `Bootstrap.debugUrl` reproduces it without the window.

## 16. Shader switcher & studio shaders (iteration 5, 2026-07-15)

A "Shader" section at the top of the outfit pane with 3 selector buttons (always visible for quick
access); the per-shader tuning panel below them is tucked into a collapsible **"Shader Settings"**
foldout (2026-07-17, matching the Card frame section). The selection persists (EditorPrefs
`OutfitStudio.Shader`) and is enforced on every avatar material in edit AND play mode, across
reloads, until another shader is picked. Studio-scene-gated — outside
`OutfitStudio.unity` nothing is touched.

| Button | Shader | What it is |
|---|---|---|
| DCL_Toon | `DCL/DCL_Toon` | Stock package shader — the official look, untouched. |
| DCL_Toon_Studio | `DCL/DCL_Toon_Studio` | Local unlocked copy (see below). |
| DCL_Stylized_PBR | `DCL/DCL_Stylized_PBR` | New Disney-principled stylized PBR (see below). |

### Live tuning panel (art direction)
Below the 3 buttons the outfit pane shows sliders/color fields for the **selected** shader
(stock `DCL_Toon` has none — it's the fixed official look). The knob list is defined ONCE in
`StudioAvatarShaderSwitcher` (`StudioKnobs` / `PbrKnobs`, a `StudioShaderKnob[]`) and is the
single source of truth: the window builds the UI from it, and `Apply()` pushes the values onto
every active-shader avatar material each poll + immediately on change. Values persist in
EditorPrefs keyed `OutfitStudio.Knob.{modeIndex}.{property}` (rim power for toon vs PBR are
independent entries). "Reset shader defaults" clears the current shader's keys.

Knobs are **global look controls** (rim, ambient, stylization) — deliberately not per-wearable
identity (textures/base color/gates are left alone). `_BumpScale` and `_StylizedMetalStrength`
are the exceptions: they override the per-wearable value with a global one (fine for a debug
tool; tooltip says so). A dedicated `_RimLightIntensity` scalar was **added to both studio
shaders** (neither had a rim-strength multiplier — rim was color+power only): in
`DCL_Toon_Studio` it scales `Set_RimLight` in the composition; in `DCL_Stylized_PBR` it scales
the fresnel rim term. Toon Studio knobs: rim intensity/power/mask/color, ambient GI, normal
strength, metal strength, matcap tint, matcap blur. PBR knobs add: rim sharpness, diffuse wrap,
shadow sharpness, specular softness, specular F0, sheen (+tint), clearcoat (+gloss), matcap
metal blend, metal strength, emission strength, matcap tint, matcap blur. Above the sliders both
studio shaders show a **Matcap dropdown** (the reflection texture; see the 2026-07-16 update).
Matcap blur is capped 0–4. (Metal strength/blend, emission strength, and the dialed-in default
look values: see the iteration-6 update at the end of §16.)

### How switching works — `Editor/StudioAvatarShaderSwitcher.cs`
Poll-based (`[InitializeOnLoad]`, 0.5 s on `EditorApplication.update`, ticks in play mode too —
same pattern as the overlay hider / pipeline switcher). Every avatar reload creates fresh
material clones with the stock shader; the next tick scans every renderer in the studio scene via
`Resources.FindObjectsOfTypeAll<Renderer>()` (filtered to the active scene) and acts on any whose
`sharedMaterial.shader.name` is one of the three avatar shaders. Important: it must NOT use
`FindObjectsByType<Renderer>` — that skips `HideFlags.DontSave` objects, and the edit-mode preview
builds its avatar with exactly that flag, so the scan would find zero renderers in edit mode.
`Resources.FindObjectsOfTypeAll` returns DontSave/inactive/hidden objects too, so it's independent
of avatar hierarchy and covers play-mode wearables the same way. If the target shader can't be
resolved (compile error / not imported) it logs one warning and skips; on a button click it logs
the outcome (materials found / swapped) so a no-op is never silent — `0 avatar materials` means no
outfit is loaded into the preview yet. **Swap mechanics:** named properties survive by name;
`renderQueue` resets on shader assignment (the generator sets it for cutout/transparent
wearables) and keywords are restored defensively — both saved/restored around the swap.
Materials are filtered by shader name (one of the 3 above), which naturally excludes
`DCL/DCL_Avatar_Facial_Features` (eyes/brows/mouth stay stock in all modes) — plus an
`EditorUtility.IsPersistent` guard so `Avatar_Toon.mat` can never be dirtied. In PBR mode the
carried `_GI_Intensity 0` is nudged to 1 once per swap (toon compiles ambient off; PBR needs it).

**Outline contract:** `AvatarUtils` collects outline renderers by `shader.name ==
"DCL/DCL_Toon"` at LOAD time (always before our first swap — materials are born stock), and the
outline feature draws each renderer's *current* material via `FindPass("Outline")`. So no
renderer change was needed — but **every switchable shader MUST have a pass named "Outline"**
(both new shaders do; the PBR one is gated by its `_OutlineEnabled` toggle).

### DCL_Toon_Studio — `Shaders/DCL_Toon_Studio/`
Copied from the **metallic branch** of `unity-shared-dependencies` (local clone
`/mnt/d/GIT/unity-shared-dependencies` @ `feat/toon-normalmap-stylized-metallic` = `9eda18fb`,
the exact commit the aang metallic branch pins) — so normals + stylized matcap metallic are
included without waiting for that branch to merge. Edits on top of the copy:
- Renamed `Shader "DCL/DCL_Toon_Studio"`, dropped the package-bound `CustomEditor` line, fresh
  .meta GUIDs everywhere (never reuse package GUIDs).
- **Promoted from compile-time constants to material properties** (the branch's own
  `_MatCapColor`/`_BlurLevelMatcap` promotion was the template; CBUFFER + DOTS-instancing
  mirrors in `DCL_ToonInput.hlsl`): `_RimLight`, `_RimLight_Power`, `_RimLight_InsideMask`,
  `_RimLight_FeatherOff`, `_Is_LightColor_RimLight`, `_Tweak_RimLightMaskLevel`,
  `_RimLightColor`, `_GI_Intensity`, `_BumpScale`, `_StylizedMetalStrength` (was a local
  const). `Avatar_Toon.mat` already serialized the old constant values, so the default look is
  IDENTICAL until you tweak — the promotion just makes the knobs live (the Fortnite-card rim!).
- **Re-enabled the UTS additional-lights loop** in `DCL_ToonBodyDoubleShadeWithFeather.hlsl`
  (was commented out pending a Forward+ rework). Its helpers were all still live; the
  referenced shade-map/high-color textures do NOT exist in this trimmed DCL variant, so the
  loop body was stubbed exactly like the main-light path (base-as-shademap, masks = 1). The
  studio pipeline (classic Forward, per-pixel additional lights) uses the `UTS_LIGHT_LOOP`
  path; spot/point lights now hit the avatar with banded UTS-style cel additions (that's the
  point — it's stylized, not physical).

### DCL_Stylized_PBR — `Shaders/DCL_Stylized_PBR/`
New hand-written URP shader (`.shader` + `_Input.hlsl` + `_ForwardPass.hlsl`). The OW2 GitHub
reference was inspected and discarded (Built-in RP, plain Unity Standard + texture packing);
the model is instead the **Disney Principled BRDF** (Burley SIGGRAPH 2012 — the
parameterization Unreal/Fortnite shading derives from), implemented fresh:
- Burley diffuse with retro-reflection, over a stylization layer: `_DiffuseWrap` +
  `_ShadowSharpness` (wrapped, smoothstep-sharpened falloff).
- GGX + height-correlated Smith specular; Disney `_Specular` F0 scale for dielectrics;
  `_SpecularSoftness` compression for the broad stylized gleam.
- `_Sheen`/`_SheenTint` (cloth edge gleam) and `_Clearcoat`/`_ClearcoatGloss` (GTR1 lobe —
  the glossy "action figure" finish).
- Metallic from `_MetallicGlossMap.b`, roughness from `.g` (glTF ORM, same convention as the
  toon metallic work; `_Metallic`/`_Smoothness` scalars as fallback when no map).
- Additional lights (Forward and Forward+), SH ambient via the shared `_GI_Intensity`, artist
  rim on the shared `_RimLight*` names (same exponent mapping as toon so carried values feel
  familiar, plus `_RimSharpness`), **matcap as environment reflection for metals** (SH
  fallback), emission ×2.5 to match toon's magic number, same `_IS_CLIPPING_*` dynamic-branch
  clipping contract, and ShadowCaster/DepthOnly/DepthNormals passes with alpha clip.
- `_OutlineEnabled` toggle (default on) on the mandatory inverted-hull "Outline" pass.
Property names match DCL_Toon everywhere they overlap — switching is lossless in both
directions.

### Renderer touches (prod-safe, documented)
- `Assets/Scripts/Loading/ToonMaterialGenerator.cs` + `Assets/Scripts/CommonAssets.cs`: the
  metallic branch's diffs applied VERBATIM (`git diff main...feat/avatar-toon-shader-metallic-
  normals` on those two files applies cleanly — keep it that way for a trivial future merge).
  Feeds GLB `normalTexture` → `_NormalMap`, `metallicRoughnessTexture`/`metallicFactor` →
  `_MetallicGlossMap`/`_IsStylizedMetallic`, matcap from `CommonAssets.MatcapPresets`. The
  stock package shader ignores all of these properties → play/WebGL behavior unchanged.
- Bootstrap/Main.unity NOT touched: `StudioAvatarShaderSwitcher` assigns
  `CommonAssets.MatcapPresets` (from `Shaders/Matcaps/MatcapPresets.asset`) +
  `DefaultMatcapName = "matcap_01"` on its poll instead.

### ⚠ Delete-at-integration tripwire
`Runtime/MatcapPresets.cs` is a verbatim copy of the package type (same namespace
`DCL.Rendering.DCL_Toon`, kept identical so the ported generator code needs zero edits). When
the metallic branch merges into the package and the package is repointed/updated, the duplicate
type will fail compilation with **CS0433** — that's the intentional signal to: delete
`Runtime/MatcapPresets.cs` + `Shaders/Matcaps/`, wire Bootstrap per the metallic branch, and
optionally delete `Shaders/DCL_Toon_Studio/` in favor of upstreamed unlocks.

### Verification (not yet run — needs the editor)
1. Focus Unity → both new shaders compile, no CS errors.
2. Studio scene, edit mode: apply an outfit → each button swaps all body/wearable renderers
   within ~0.5 s; facial features unaffected; 2→1 and 3→1 restores look pixel-identical
   (check hair alpha-clip edges / transparent wearables → renderQueue restored).
3. Persistence: re-apply outfit / change body shape / enter play / restart editor → selection
   re-applies.
4. Studio mode: `_RimLight_Power` etc. live-tweakable on a material instance; spot/point
   lights affect the avatar; `_GI_Intensity > 0` lifts ambient; a metallic wearable shows
   matcap metal.
5. PBR mode: normals shade; metallic masks specular; clipped hair correct in shadows/depth;
   outline toggle works.
6. Prod safety: no diffs on `Avatar_Toon.mat` / `Main.unity` / `Bootstrap.cs` / manifest.

### Update 2026-07-16 (iteration 6) — stylized-metal fixes + matcap controls (CONFIRMED working)

First real in-editor test of stylized metallic via **Load from Collection** (draft PuffyJacket,
the same asset QA'd in unity-explorer). Normals rendered but metal didn't; three fixes landed,
all in studio-only code (the verbatim `ToonMaterialGenerator` was NOT touched):

1. **The metal gate never opened (root cause).** The switcher's diagnostic (see below) showed the
   jacket material with `_MetallicGlossMap`/`_MatCap_Sampler` bound, both `*Arr_ID = 0`, but
   `_IsStylizedMetallic = 0` — so the shader gate `_IsStylizedMetallic > 0 && _MatCap_SamplerArr_ID
   >= 0` stayed shut (normals were never gated on it, hence "normals yes, metal no"). Cause:
   avatar materials are born on the **stock `DCL/DCL_Toon`** package shader, which on this branch
   does NOT declare the metallic-branch `_IsStylizedMetallic`. Setting a real `Integer` property
   the *active* shader doesn't declare does not survive the later `mat.shader` swap to the studio
   shader — it falls back to the shader default (0). (The neighbouring `_MetallicGlossMapArr_ID`
   DID survive, because stock DCL_Toon declares it.) **Fix:** in `StudioAvatarShaderSwitcher.Apply`,
   after the swap, re-assert `_IsStylizedMetallic = (_MetallicGlossMapArr_ID >= 0) ? 1 : 0` — using
   the surviving mask id as the "metal was detected" signal, now that the active shader declares the
   flag. Data-driven, so it's correct regardless of the exact persistence mechanism. This also fixed
   DCL_Stylized_PBR (same gate).
2. **Matcap selector + live tint/blur.** New `ActiveMatcapName` (EditorPrefs `OutfitStudio.Matcap`)
   + a **Matcap dropdown** at the top of the tuning panel (both studio shaders; names from
   `GetMatcapNames()` over the loaded library). The switcher pushes the selected matcap **texture**
   onto metal materials each poll (`_MetallicGlossMapArr_ID >= 0` signal) so switching is live.
   `_MatCapColor` (tint) and `_BlurLevelMatcap` (blur, capped 0–4 in knobs AND both shaders' Range)
   are now **tuning knobs on both shaders** — so the knob loop owns them and the matcap push sets
   texture-only (preset tint/blur no longer applied live; all presets are white/0 anyway).
   `EnsureMatcapPresets` now seeds `CommonAssets.DefaultMatcapName` from `ActiveMatcapName`.
3. **PBR metal now matches DCL_Toon_Studio.** PBR was adding the matcap as a Fresnel/F0-weighted
   reflection (`color += envRefl * envF * metallic * ...`) → bright only at grazing edges, tinted by
   the dark metal albedo, layered over a diffuse-free (dark) base = dark jacket with lit edges. Toon
   does a flat *replace*. Rewrote the PBR metal block (`DCL_StylizedPBR_ForwardPass.hlsl`) to
   `reflWeight = lerp(envF, 1, _MatcapMetalBlend); color = lerp(color, envRefl*reflWeight,
   saturate(metallic) * _StylizedMetalStrength)`. So `_MatcapMetalBlend` is now a **physical(0) ↔
   flat/toon-match(1)** dial and `_StylizedMetalStrength` (added to the PBR shader: cbuffer +
   Property `Range(0,4)`) is the replace amount (1 = full, matches toon; >1 over-drives, like toon).
   Defaults (blend 1, strength 1) match toon out of the box. Only remaining gap vs toon: toon also
   multiplies the matcap by the main light colour; PBR doesn't (invisible under a ~white key).

4. **Rim on metal (toon).** In `DCL_Toon_Studio` the rim is baked INTO `finalColor` (via
   `_RimLight_var = Set_HighColor + Set_RimLight * _RimLightIntensity`), so the metal replace-lerp
   (`finalColor = lerp(finalColor, matcapRefl, metalFactor)`) wiped the rim out on metal areas —
   metal jackets got no rim while cloth did. PBR was fine (it adds rim *after* the metal). Fix in
   `DCL_ToonBodyDoubleShadeWithFeather.hlsl`: after the metal lerp, add the rim term back on top,
   `finalColor += rimTerm * saturate(metalFactor)` (rimTerm = the same `lerp(0, Set_RimLight *
   _RimLightIntensity, _RimLight)`), so metal catches the rim too; non-metal is unchanged.

5. **Emission Strength (PBR).** PBR emissives read much hotter than toon under the studio's HDR
   bloom — NOT because emission differs (both shaders use the identical `_Emissive_Tex *
   _Emissive_Color * 2.5`), but because PBR's emissive pixels sit on a brighter additive base
   (ambient on + additive rim on the same silhouette edges), so more of them cross the bloom
   threshold. Bloom is off-limits, so a `_EmissionStrength` scalar was added to the PBR shader
   (cbuffer + Property + multiplied into the emissive term) and exposed as the **Emission Strength**
   knob. **Default 0.19** — the value that visually matches DCL_Toon under the studio bloom.

Updated knob lists: **Toon Studio** adds Matcap Tint + Matcap Blur; **PBR** adds Metal Strength,
Emission Strength, Matcap Tint + Matcap Blur (and the Matcap Metal Blend tooltip now describes the
physical↔flat dial).

**Dialed-in default look (2026-07-16, confirmed by Mauricio).** The knob defaults were tuned to a
finished look so a fresh studio scene reads right without fiddling. Shared: rim tint `#CCB777` warm
gold (a single `RimGold` field in `StudioAvatarShaderSwitcher`, referenced by both shaders).
- **DCL_Toon_Studio:** Rim Intensity 10, Rim Power 0.8, Rim Inside Mask 0.5, Rim Color gold.
- **DCL_Stylized_PBR:** Rim Color gold, Diffuse Wrap 0.5, Shadow Sharpness 0.55, Specular Softness
  2.2, Specular (F0) 0.4, Sheen Tint 0, Ambient (GI) 2.5, Emission Strength 0.19 (others unchanged:
  Rim Intensity 1 / Power 0.3 / Inside Mask 0.15 / Sharpness 0, Metal Strength 1, Matcap Metal
  Blend 1, Matcap Blur 0, Normal Strength 1).
Changing a C# default does NOT move a knob whose value is already stored in EditorPrefs — press
**Reset shader defaults** for that shader once to adopt new defaults; fresh installs get them.

**Diagnostic** (kept, verbose-only): `Apply(verbose:true)` — fired on a shader **button click** —
dumps per-material metal-gate state (`_IsStylizedMetallic`, `_MatCap_SamplerArr_ID`,
`_MatCap_Sampler` bound?, `_MetallicGlossMapArr_ID`, `_MetallicGlossMap` bound?, strengths) plus the
`MatcapPresets` load state. All reads are `HasProperty`-guarded (toon vs PBR expose different
props). The per-material lines are in the log entry's expanded detail pane, not the collapsed list.
Note: the "No MatcapPresets assigned" warning only fires if metal was *detected* (it's inside
`ApplyDefaultMatcap`), so its absence is not proof the library is loaded — read the dump's
`MatcapPresets=N presets` header instead.

**Blur caveat:** `_BlurLevelMatcap` samples the matcap by mip LOD, so it only softens visibly if the
6 matcap PNGs are imported **with mipmaps enabled** — check their import settings if blur looks inert.

**Outline color/width promoted to runtime properties + made flat (2026-07-24, "Outlines fixes").**
Stock `DCL_Toon`'s outline was compile-time constants (`DCL_ToonVariables.hlsl`:
`#define _Outline_Width 2.0f`, `#define _Outline_Color float4(0.632,0.632,0.632,1)`, uneditable —
the inverted-hull outline mechanism itself is: the outline pass extrudes the mesh outward along
vertex normals by `_Outline_Width` and draws its backfaces flat-shaded in `_Outline_Color`, so a
width too thin gets eroded into the background by SMAA before it ever reaches the outline color)
and was tinted by the garment's own albedo
(`_Is_BlendBaseColor` lerp: `Outline_Color * albedo² * lightColor`), which crushed any chosen color
into a dark, garment-dependent sliver. Both studio shaders now declare `_Outline_Width`,
`_Outline_Color`, `_Is_BlendBaseColor` as real per-material properties (DOTS-instanced in
`DCL_ToonInput.hlsl` alongside the other promoted knobs) and render `_Outline_Color` **literally** —
`DCL_ToonOutline.hlsl`'s fragment is now just `_Outline_Color.rgb * lightColor` (drop the
albedo-blend lerp entirely); PBR's outline pass (previously a stock verbatim copy) matches: `return
half4(_Outline_Color.rgb, 1)` instead of tinting by `_BaseColor`/`texColor`. New knobs on **both**
shaders: **Outline Width** (0–10, default 3 — wider than stock's 2 so the stroke survives the
camera's antialiasing instead of being eroded into the background) and **Outline Color** (default
burnt-orange `#B85C2A`, a shared
`OutlineOrange` field in `StudioAvatarShaderSwitcher` next to `RimGold`).

**Eligibility check fixed for re-shaded materials.** `AvatarUtils.cs`'s outline-renderer gate
matched `sharedMaterial.shader.name == "DCL/DCL_Toon"` literally — but the shader switcher re-shades
avatar materials **in place** to `DCL_Toon_Studio`/`DCL_Stylized_PBR` while the game is running, so
this check (re-run on every reload) silently stopped finding any eligible renderer once a studio
shader was active. Fixed to `sharedMaterial.FindPass("Outline") >= 0` — both studio shaders declare
that pass — so the outline keeps working across shader switches. (Prod default `DCL_Toon` is
unaffected: it also declares an "Outline" pass, so `FindPass` still matches it the same as before.)

**Live antialiasing override for outline debugging:** `StudioCardFrame.DebugAntialiasing` (nullable
`AntialiasingMode`, not persisted, play-mode only — the actual rendering camera only exists once
play mode spins up the scene) lets you compare None/FXAA/TAA/SMAA live against the wider outline
stroke, re-applied every 0.5 s poll (`SyncDebugOverrides`) so it survives a play-mode re-entry.
Restores the camera's original antialiasing when cleared.

**Still-capture outline gap fixed:** `RendererFeature_AvatarOutline`'s renderer list is repopulated
every frame by `AvatarLoader.Update` and cleared after each camera render; a still capture's extra
render (issued after the Game view's own render, same frame) previously found an already-emptied
list and drew no outline even though the live Game view showed one. `AvatarLoader.RefreshOutlineRenderers()`
re-populates it from `_loadedModels` right before the capture's render (both the old
`SubmitRenderRequest` path and the current Recorder-based one call it — see §8). Also added:
`AvatarLoader.MainCamera` (public accessor), `GetBone(name)`/`HeadBone`/`NeckBone` (exact glTF node
name match, `"...Head"`/`"...Neck"` suffix fallback), and `FreezePose()` (`avatarAnimation.Stop()`) —
all consumed by the **Look at Camera** capture control, see §8.

**Card frame ZTest fix:** the card panel quad (queue 1500) used `ZTest Always`, so it painted over
the avatar outline's near-depth ring (the outline pass draws `BeforeRenderingOpaques` and writes
depth in its stroke) — the outline showed the card's color instead of the outline color wherever it
crossed the card. Changed to `ZTest LessEqual`: the card still draws over the BG quad (same far Z)
but now respects the nearer outline ring and the opaque avatar. See §18 for the rest of the card
frame's alpha/layout fixes from the same pass.

## 17. Screenshot poses (iteration 6, 2026-07-16)

Single-frame "poses" for stills: drop GLBs (1-frame skeletal animations) into
**`Assets/OutfitStudio/Poses/`** and a **button per pose** appears under the **Pose** header
(`OutfitStudioWindow.BuildPoseButtons` / `GetPoseNames`), auto-discovered by a file scan of
`Application.dataPath + "/OutfitStudio/Poses"`. Click → sets `outfit.emote`, clears any draft emote,
applies; the active pose's button is disabled (= selected, same convention as the shader buttons);
a `⟳` button rescans the folder without reopening the window.

**Kept inside the tool folder with ZERO renderer changes** (the whole point — no files spilled into
StreamingAssets). Poses ride the stock embedded-emote path: the emote name is
`"../OutfitStudio/Poses/<file>"`, and `Representation.ForEmbeddedEmote` resolves it as
`Path.Combine(streamingAssetsPath, name + ".glb")` = `.../Assets/StreamingAssets/../OutfitStudio/
Poses/<file>.glb`. The `..` walks back out of StreamingAssets into the tool folder; the OS/URI
normalises it when the loader opens the file (same bare-path handling the built-in emotes use). The
name is project-relative, so it's machine-independent (share codes / persistence work for any
teammate with the same `Poses/` folder), and because it points outside StreamingAssets it never
resolves in production builds — which is fine, poses are an editor-only screenshot tool.

**Fix (2026-07-22): transport (▶/❚❚/■) snapping back to the last pose instead of a picked embedded
emote.** The "Embedded" popup shared `outfit.emote` with the pose buttons but was only built once
(`BuildOutfitPane`, on window open) and computed its displayed index as
`EMBEDDED_EMOTES.IndexOf(outfit.emote)` — a pose path isn't in that list, so the popup silently fell
back to showing `"idle"` (index 0) after any pose click, while the pose was what was actually
loaded/playing. If the user then "reselected" whatever the popup already showed (commonly `"idle"`),
UI Toolkit's `PopupField` doesn't fire `RegisterValueChangedCallback` for a same-value pick, so
nothing reloaded — the transport buttons kept controlling the stale pose clip: Play looked like a
no-op (a 1-frame pose is already "playing"), Stop crossfaded the pose to idle, and Play again
replayed the pose, not the emote the popup claimed to show. Fixed by giving the popup a sentinel
choice, `EMBEDDED_EMOTE_NONE`, and a `SyncEmotePopup()` helper (`_emotePopup.SetValueWithoutNotify`)
called from every place that sets `outfit.emote` outside the popup itself — pose buttons, draft/
catalog emote picks (`EquipDraft`, `OnItemClicked`), and `LoadOutfit` (share code / preset loads).
Whenever a pose (or anything else not in `EMBEDDED_EMOTES`) becomes active, the popup now visibly
shows the sentinel instead of a stale `"idle"`/emote name, so picking an actual embedded emote
afterwards is always a genuine value change and reliably reloads + auto-plays it.

**Apply in play mode** (like all emotes — a 1-frame emote holds its frame): equip → Enter Play →
click a pose → Capture Still. Edit mode still shows the static idle pose (poses aren't sampled onto
the edit-mode skeleton).

**Play-mode pose buttons change ONLY the pose, not the loaded avatar (2026-07-17).** In play mode a
pose button calls `ApplyPoseOnly` instead of `Apply`: it sets just `PreviewConfiguration.Emote` and
reloads, so whatever avatar is loaded keeps its identity/wearables and only the pose changes (the
`AvatarLoader` diffs the unchanged wearables, so just the emote reloads) — mirroring how the shader
switcher edits the loaded avatar rather than reloading it.

**Mode handling (important):** `Builder` (the custom outfit) is kept. **Every other mode is switched
to `Profile`** (preserving `config.Profile`, so a Debug-tab **Random Profile** stays the same avatar,
now posed). This is required because `LoadForProfile`/`LoadForBuilder` pass `config.Emote` through
`FromEmbeddedEmote` (so `"../OutfitStudio/Poses/<file>"` resolves), **but `Jesus` mode hard-codes its
emote** (`character/Particles_Anim` — the arms-out "jesus" pose) and Marketplace shows a wearable —
both ignore `config.Emote`. Random Profile via `SetProfile` doesn't change the mode, so if the
session was in Jesus mode the pose silently wouldn't apply; forcing Profile fixes it.

**Holding the pose (`EmoteLoop`):** a single-frame pose only *holds* if the emote loops. Builder's
`ResolveBuilderEmote` uses `loop: true`, but `LoadForProfile`/`LoadForMarketplace` use `loop: false`
— so in Profile mode the 1-frame pose ended instantly and reverted to the base breathing idle.
`ApplyPoseOnly` sets `config.EmoteLoop = true` (renderer touch point #6, prod default false) so the
profile pose holds. **Edit mode is unchanged** (pose buttons route through `Apply`). Constants live
in `OutfitStudioWindow`: `POSES_DIR_UNDER_ASSETS` (`OutfitStudio/Poses`, for the scan) and
`POSES_EMBEDDED_PREFIX` (`../OutfitStudio/Poses`, the emote name). A future v2 could sample the pose
clip onto the edit-mode skeleton (like `SampleIdlePose`) so shots can be framed without entering play.

**Update 2026-07-22: the "Embedded" emote popup now goes through `ApplyPoseOnly` too, in play mode.**
Previously picking an animation ("dance", "clap", ...) from the popup always called the full `Apply`,
which hardcodes `config.SetMode("builder")` — so choosing an animation forced a reload of the
studio's custom outfit, discarding whatever avatar was actually loaded (e.g. a Debug-tab Random
Profile), exactly the outfit-switch the pose buttons were built to avoid. The popup's change handler
now mirrors the pose buttons: `Application.isPlaying` → `ApplyPoseOnly(outfit.emote)` (re-animate the
loaded avatar in place); otherwise → `ScheduleApply()` as before (edit mode has no avatar to preserve
identity for). `ApplyPoseOnly` itself is generic over "single-frame pose" vs. "multi-frame animation"
— both are just an embedded-emote name reaching `FromEmbeddedEmote` through Profile/Builder mode, so
no changes were needed there beyond documentation.

**Same-day follow-up: the "Emotes / Poses" catalog tab had the same bug, plus a real renderer gap.**
`OnItemClicked`'s emote branch (catalog tiles built by `BuildTile`/`RunSearch`, i.e. the "Emotes /
Poses" tab) unconditionally fell through to the shared `RefreshShareCode(); ScheduleApply();` at the
bottom of the method — the same forced-Builder-reload bug as the popup, now fixed the same way:
`Application.isPlaying` → `ApplyPoseOnly(outfit.emote)`, else the old `ScheduleApply()` (early
`return` right after; the wearable-equip branch below is unchanged and still always does the full
Apply, which is correct — equipping a wearable *should* go onto the custom outfit).

Unlike the pose buttons / Embedded popup, catalog tiles set `outfit.emote` to a real published
**URN** (`item.urn`), not an embedded-clip name — and that exposed **renderer touch point #7**:
`PreviewController.LoadForProfile` unconditionally called `EntityDefinition.FromEmbeddedEmote
(defaultEmote, loop)`, treating ANY string (including a URN) as an embedded StreamingAssets clip
name. Builder mode already had URN handling (`ResolveBuilderEmote`'s `StartsWith("urn:")` branch,
fetching the real entity via `EntityService.GetEntities`), but Profile mode never needed it in
production (the `emote=` query param is documented as one of the embedded names only) — so
`ApplyPoseOnly`'s "force Profile mode unless already Builder" would have silently failed to load a
catalog emote picked while posing a Random Profile. Fixed by giving `LoadForProfile` the same
`StartsWith("urn:")` branch as `ResolveBuilderEmote` (fetch via `EntityService.GetEntities`,
otherwise fall back to `FromEmbeddedEmote` exactly as before — purely additive, no behavior change
for any existing non-URN caller). Touch point in `Assets/Scripts/Preview/PreviewController.cs`.

## 18. Card frame — Fortnite-style item cards (2026-07-17)

A "Card frame (beauty shot)" section (collapsible Foldout at the top of the outfit pane) composites
a Fortnite item-card look around the avatar: **rounded card panel → avatar → bottom fade → border**
*(the border is behind the avatar as of 2026-08-06 — it's last in the queue but depth-tested)*,
with **nothing behind it** (see the 2026-07-30 background-removal entry below — the original design
had a fullscreen gradient backdrop, and the sections here describe the current, backdrop-less state).
The reference targets are the marketplace/Fortnite item cards (head overflowing the top edge, legs
fading into the card); the card itself is painted with the Decentraland loading-screen
vignette/pattern. Studio-scene
only; **fully folder-isolated — zero renderer-data / shipping-asset edits, and nothing ships to a
build** (the shader is only referenced by editor-created runtime materials, so it's excluded from
the WebGL build — verify in a build report, same discipline as the Nethereum DLLs in §13).

### Why quads, not a UI overlay or a renderer feature
The hard constraint is §8: **runtime UI overlays don't render through the capture camera**, so the
frame must be camera geometry to appear in the exported PNG. Two ways to get camera geometry:
- A URP fullscreen renderer feature (like `BackgroundRendererFeature`) — but the studio
  **PreviewCamera renders through `URP_PreviewRenderer` (renderer index 1)**, which has only the
  outline feature; the gradient `BackgroundRendererFeature` lives on `URP_ConfiguratorRenderer`
  (index 0 = the ConfiguratorCamera we strip). Adding a feature would mean editing a **shipping**
  renderer data (or duplicating it, §14 "Route A") **and** shipping the shader into prod builds.
- **Camera-parented quads** (chosen) — keeps everything under `Assets/OutfitStudio/`, needs no
  renderer/asset changes, and the quad shader never enters a build.

### Layers (ordered by render queue, so no per-avatar depth math)
`StudioCardFrame` ([InitializeOnLoad], 0.5 s poll, studio-scene-gated like the other helpers)
parents four quads to the render camera (`Camera.main`, matching what `OutfitCapture` uses; falls
back to a `PreviewCamera`/highest-depth search). One shader (`Custom/StudioCardFrame`, `_Mode`
0/1/2/3) with **material-driven render state** (`_ZTest`/`_ZWrite`/`_SrcBlend`/`_DstBlend`, plus a
separate `_SrcBlendA`/`_DstBlendA` pair for alpha) covers all four:
- **Card panel** (`_Mode 0`) — queue 1500, ZTest LessEqual, **ZWrite On**, alpha blend. Rounded-rect
  (SDF, aspect-corrected) painted with the Decentraland vignette + scrolling icon pattern (fill only
  — the border is its own top layer, below). Drawn **before** the avatar (opaque, queue 2000), so the
  avatar draws over it and the **head overflowing the top edge is free** (no masking — that was the
  original "avatar mask" worry; it dissolves because the card is just a shape *behind* the avatar).
  No hard side-clip by design — framing + margins keep the avatar inside, matching the refs (add the
  SideMask toggle below for a hard clip). It is the **only depth-writing layer**, inheriting that job
  from the deleted background quad: without a ZWrite-On layer the skybox (drawn after the opaque
  queue) paints straight over the card in any view whose clear is Skybox, e.g. the Scene view. The
  shader `clip()`s the card's fully-transparent pixels so only the rounded rect writes depth, leaving
  the four corner notches clear. Safe because the studio renderer has `m_DepthPrimingMode: 0`
  (priming would force ZTest Equal and skip a quad with no DepthOnly pass) and Forward+
  (`m_RenderingMode: 2`) is fine for unlit quads. **ZTest LessEqual, not Always**, so the avatar
  outline's near depth (written BeforeRenderingOpaques) isn't painted over by the card.
- **Bottom fade** (`_Mode 1`) — queue 3500, ZTest Always, **ZWrite On (2026-08-06)**, alpha blend. Drawn
  **after** the avatar; same rounded rect as the card (so its bottom corners match) × a vertical fade to
  transparent. It samples **the card's own paint at the same UV** (the two quads share a transform), so
  it's a pure alpha ramp over an identical colour — seamless by construction, with no fade colour to keep
  in sync. The ZWrite is not for occlusion — see "Depth honesty" under Border below.
- **Border** (`_Mode 3`) — queue 4000, ~~ZTest Always~~ **ZTest LessEqual (2026-08-06)**, alpha blend.
  ~~Drawn **last, on top of everything** (avatar, fade, side-mask) so the card outline is never
  occluded~~ — Mauricio: *"card outline is rendering over the wearables, should be behind like the card
  background."* It is still **last in the queue**, so it still wins over the bottom fade and the side
  mask; only the depth test changed, so **the avatar occludes it** exactly like it occludes the card
  panel. The two are independent axes, which is the whole reason this was a one-line fix: dropping the
  border to a pre-avatar queue instead would also have put it behind the fade, and the fade paints the
  card's own colour over the card's lower region — it would have eaten the bottom edge of the ring.
  Why it works with no new depth math: the border quad already sits at `PLANE_Z = 50`, far behind a ~2 m
  avatar, so `Always` was the *only* thing keeping it on top. At queue 4000 the depth buffer already
  holds `min(card 50, avatar ~2)` per pixel, so the ring passes inside the card rect (equal), passes
  outside it (the card clips there, leaving the far clear value), and fails wherever the avatar is. It
  also now respects the avatar **outline** ring, which writes near depth in `BeforeRenderingOpaques` —
  the same reason the card panel uses LessEqual rather than Always (see the `MakeQuad` comments).
  **Depth honesty, the follow-up the same day.** Mauricio: *"mask avatar below card mask the outline which
  shouldn't happen."* Making the border depth-tested exposed a second-order problem: **erasing or
  repainting colour does not clear depth.** The side mask wipes the avatar to transparent and the bottom
  fade paints the card's own colour over the legs, but both left the avatar's near depth in the buffer — so
  geometry that was no longer visible kept occluding the ring, punching gaps in it wherever a leg crossed
  the card's bottom edge. Fix: **Fade and SideMask now run ZWrite On**, and each shader path `clip()`s the
  fragments it doesn't actually cover (mode 2 discards its keep-region, mode 1 its fully-transparent
  pixels — both no-op blends, so nothing changes visually), which lands the depth reset on exactly the
  region each layer repaints, at `PLANE_Z`, the same plane the border sits on. ZWrite on a post-avatar
  transparent layer reads oddly, which is why both call sites say why. The rule it enforces: *the avatar
  occludes the border wherever the avatar is still visible* — not wherever it happened to be drawn. One
  deliberate consequence: inside the fade's **gradient** the leg is still partly visible and the border now
  draws in front of it, because a continuous ring reads better than a stroke dipping behind a half-faded
  shin (and it matches the pre-2026-08-06 look at the bottom).

  Consequences worth knowing: a border crossing a **transparent** wearable can still show through it
  (transparent geometry doesn't write depth — the card panel has always had this property too), and
  `_BorderTopFade` / **Closed border** are unaffected, since they're about the ring's own shape rather
  than its layering. The border remains a separate quad rather than being baked into the card panel,
  now for a different reason: the outer ring extends past the card rect, where the panel has no paint.
  Two rings straddle the card edge
  (`dist == 0`): an **inner** ring in the band `dist ∈ (-_InnerBorderWidth, 0)` and an **outer** ring
  in `dist ∈ (0, _OuterBorderWidth)` that extends past the card into the avatar/fade/side-mask layers
  beneath. Each is built as `saturate(sInner - sInnerCutoff)` / `saturate(sOuterCutoff - sOuter)`
  (difference of edge smoothsteps straddling the band) so it collapses to **exactly** 0 at width 0 —
  do **not** revert to `mask * innerCut`, which peaks at ~0.25 on the edge and leaves a ~1px hairline
  around the whole card even at width 0. The two are summed and saturated into one `ring` value.
  **Cutoff bias (2026-07-28 fix):** a naive shared cutoff at exactly `dist == 0` for both rings only
  reaches 50% coverage right on the seam (correct AA for a hard edge) — fine when both rings are
  active (their two 50%s sum to 1), but with only ONE width non-zero the other 50% was never filled
  in, showing whatever's underneath (avatar/background) through as a ~1px line at the card edge. Each
  ring's cutoff is now nudged ~1px (`aa`) past `dist 0` into the *other* ring's territory
  (`sInnerCutoff = smoothstep(-aa, aa, dist - innerBias)`, `sOuterCutoff` mirrored with `+outerBias`),
  where `innerBias`/`outerBias` ramp from 0 (at width 0, keeping the cutoff identical in form to the
  ring's own smoothstep so the exactly-0 cancellation still holds) up to `aa` for any usable width.
  **Open at the top**: both rings fade out above `_BorderTopFade` (uv.y 0.88) so the border frames
  only the sides + bottom and the head overflows the top freely (without this the top edge draws
  across the neck/shoulders — same intent as the side-mask leaving the top open).
  **Outer-ring quad sizing**: the SDF only has values to sample for `dist > 0` if the Border quad's
  own mesh physically extends past the card rect, so (unlike Card/Fade) the Border quad is scaled up
  by `_BorderOversize` around the same centre (`StudioCardFrame.Layout()`), sized from the card's
  aspect so the tightest reach direction still clears the slider's max width plus AA slack. The
  shader remaps the Border quad's raw UV back into the card's normalized SDF space
  (`uv = (uv - 0.5) * _BorderOversize + 0.5`, mode 3 only) before the shared `dist` calculation below,
  so `dist == 0` still lands exactly on the card edge and `topOpen`'s `uv.y` check still means the
  same thing.

The card, fade, and border quads share the same rect; only the side mask is fullscreen. Placement Z
is a fixed `PLANE_Z = 50` (behind a ~2 m avatar, inside the far plane); ordering is queue-only so the
exact Z doesn't matter except for the depth writes. *(2026-08-06: it matters slightly more now — card,
fade and mask all write depth at `PLANE_Z` and the border depth-tests against it, so the fact that
**all four quads sit at exactly the same Z** is what makes the border's `LessEqual` pass on equality
inside those regions. Moving one layer's Z independently would silently break the ring.)*

### Optional avatar clipping — `SideMask` / `BottomMask` toggles
Drawing the card behind the avatar gives top-overflow for free but no clip anywhere else.
**"Mask avatar to card sides"** and **"Mask avatar below card"** — both **on** by default as of
2026-07-30 — drive one quad (`_Mode 2`, queue 3200 — in front of the avatar + transparent wearables,
before the fade and border so it can't erase either) that **erases everything outside the masked edges,
leaving the top open** so the head still overflows (matches the Fortnite cards, where arms/hands are
clipped at the card edge but the head pokes out the top). Only the top is unclipped: a subject poking
out of the sides or bottom reads as a mistake, where the head overflowing the top is the look itself.
⚠ Both defaults only apply to an Editor that has never toggled them — `EditorPrefs` keeps whatever was
last set, and "Reset card defaults" deliberately doesn't clear the toggles (it resets the card's paint
and geometry, not the on/off switches).
- **Disable Middle Card clears both.** Cropping the avatar to a card that isn't drawn is never what you
  want, and the crop is invisible in the live view until you export. The coupling lives in the
  `DisableMiddleCard` setter (so any caller gets it, and it costs one `Refresh()`, not three); the
  window re-registers that toggle's callback *after* the two mask toggles exist so it can
  `SetValueWithoutNotify` them — otherwise the checkboxes would sit ticked while the crop was off.
  **One-way on purpose:** turning the card back on doesn't silently restore them (that would mean
  hidden remembered state), you re-tick.
- **Both toggles share one rect, not one branch each.** The shader keeps what's inside `_MaskRect`
  and erases the rest; `PushParams()` builds that rect by starting from the card and pushing the
  edges you're *not* masking half a frame out (`l = -0.5, r = 1.5` / `b = -0.5`), so an unmasked edge
  simply has no geometry near the frame to cut against. Sides-only, bottom-only, both and neither all
  fall out of the same code. **`_CardAspect` and `_CornerRadius` have to be restated for that rect**
  (both are expressed relative to whatever rect the SDF is working on): the aspect scales with the new
  w/h ratio and the radius — which `RoundedBoxSDF` measures in half-heights — shrinks by exactly the
  factor the rect grew. That keeps the corners at the card's *physical* size, which is what makes the
  mask's edge land on the card's own AA edge where the two coincide (verified: physical corner radius
  is identical in all three configurations, and "both on" reproduces the pre-split aspect/rect
  exactly). The top edge is always the card's — the head-overflow column below owns that one.
- **Erases, doesn't repaint (2026-07-30):** it used to repaint the background gradient over that
  region, which needed the mask quad to carry a pixel-identical copy of the background's paint. With
  the background layer gone there's nothing to repaint *with*, so the quad's material now uses
  `Zero / OneMinusSrcAlpha` on **both** blend pairs — i.e. `dst *= (1 - srcAlpha)` for colour *and*
  alpha — and the shader writes `alpha = 1` outside the card. Colour and alpha both go to 0 there, so
  the region ends up exactly as transparent as the untouched frame around it (this is why alpha needed
  its own material-driven factor pair; every other layer keeps the standard `One / OneMinusSrcAlpha`).
- The mask quad spans the whole frame, scaled `MASK_OVERSIZE = 1.04` past the frustum so no edge
  sliver survives an aspect mismatch. The rect is handed to the shader as `_MaskRect (l,r,b,t)`
  in that quad's UV space (`U(f) = 0.5 + (f-0.5)/MASK_OVERSIZE` maps a viewport fraction into it).
- **Shape:** the same rounded-rect SDF as the card gives clipped **sides + rounded bottom corners**;
  the region **above the card top, within the card width** is forced open (`saturate(cardMask +
  withinX*aboveTop)`) so the head overflows. **Use `+`, not `max`:** at the card-top transition both
  terms are mid-fade (~0.5) with different AA widths, and `max(0.5,0.5)=0.5` dipped the keep-region
  below 1, painting a faint bg **seam across the head**; the sum is ~1 there (the terms are
  complementary in y, and `aboveTop` is 0 below the top so they never over-add elsewhere). Bottom
  corners align with the card panel; the bottom fade draws over the inside afterwards.
- Chosen over a stencil mask (would need every avatar shader to opt in) or a fullscreen composite
  (would need the avatar isolated to its own RT). The erase-outside approach needs neither and
  stays in the quad model. Only enabled when the toggle is on (`_mask.enabled = SideMask`).

### Controls & persistence
All knobs live on `StudioCardFrame` as EditorPrefs-backed static properties (keys
`OutfitStudio.Card.*`); the window builds fields from them (`BuildCardFrame`/`BuildCardBody`),
setters push live. Groups: **Card** (inner/outer vignette colour, **Pattern** texture, side/top/bottom
margins, corner radius, border colour + inner/outer width) and **Bottom fade** (height, softness).
**Pattern** (2026-07-30, gated 2026-07-31) is an `ObjectField` over `StudioCardFrame.PatternTexture`,
which persists the chosen texture's **asset GUID** (not its path — renaming/moving the asset mustn't
break the reference) and resolves back to the bundled `DclBackgroundPattern.png` when the key is empty
or the GUID no longer resolves (still true whenever the pattern is *on*: assigning a texture always
turns it back on). A replacement must import with **Wrap Mode = Repeat** or it clamps into streaks at
the card edges.

**No pattern at all (2026-07-31):** setting the field to **None** now genuinely removes the pattern —
just the Inner/Outer vignette — rather than reverting to the bundled default the way it used to
(Mauricio: selecting None always redrew the Decentraland pattern, which wasn't what "None" should mean).
This needed a real shader gate, since the luminosity blend has no neutral texture value that reads as
"no pattern" — a flat/black texture still tints the vignette. `_DclPatternEnabled` (0/1, multiplied
straight into `overlay.a` in `DclCardPaint`) is the gate; `StudioCardFrame.PatternEnabled` is the
EditorPrefs-backed switch behind it (`OutfitStudio.Card.PatternEnabled`, default **true** so a fresh
install still shows the bundled pattern). `PatternTexture`'s setter flips it as a side effect (null →
off, an asset → on) so the ObjectField alone drives both; `CardColorPreset` gained a matching
`patternEnabled` bool (default **true**, so presets saved before this field existed keep showing
whatever pattern they had) and `ApplyCardPreset` sets it *after* `PatternTexture`, since assigning
`p.pattern` (possibly null, meaning "bundled default" on old presets) would otherwise stomp the correct
enabled state via that same side effect. "Reset card
defaults" clears the keys (including `PatternEnabled`, so a reset always comes back with the pattern
on). **Defaults were re-baselined 2026-07-30** to the look Mauricio dialled in
(replacing the 2026-07-17 ones): vignette `#BF00FF`→`#4D0080` (unchanged — the reference material's
pair) and border `#FF8158`. The geometry defaults were then restated twice the same day as the px work
landed (§20) — first into frame-height units, then when side margins became a card width — and now read:
**card width 0.55**, margins **0.12** top / **0.05** bottom, corner radius **0.0332**, inner border
**0.00332** / outer **0**, fade height **0.166** / softness **0.7**, every one of them a fraction of the
capture height (card width included). That's a card of aspect 0.55/0.83 ≈ **0.66** — the portrait
item-card shape the frame was designed around — and it renders identically at any capture aspect.
Toggles, top to bottom: master **Enable** (default off, opt-in), **Disable Middle Card**, **Mask avatar
to card sides**, **Mask avatar below card**, **Hide avatar outline**.

### Capture
`OutfitCapture.CaptureStill` forces `camera.aspect = width/height` and calls
`StudioCardFrame.RelayoutFor(camera)` before the render request (then `camera.ResetAspect()` after),
so a still at a resolution different from the Game view still frames the card correctly. Set the
Game view to a **portrait aspect (~2:3)** to author WYSIWYG.

**2026-07-23 fix:** `RelayoutFor` originally only called `Layout(cam)`, not `PushParams()` — so
`_CardAspect`/`_CornerRadius` (used by the card/fade/border rounded-rect SDF) stayed evaluated for
the *Game view's* aspect even when the capture resolution's aspect differed, opening a visible seam
between quads **only in the capture**, never live. Fixed by having `RelayoutFor` call `PushParams()`
too.

**2026-07-23 alpha-export fix:** the shader used one shared `Blend [_SrcBlend] [_DstBlend]` factor
pair for RGB *and* alpha. For an anti-aliased edge composited over an already-opaque layer beneath
(the common case once the BG quad has drawn), that blends alpha as `srcAlpha² +
dstAlpha·(1-srcAlpha)`, which dips as low as ~0.75 instead of staying at 1 — invisible in RGB (the
painted color already matches what's underneath) but a visible seam **in the alpha channel alone**,
e.g. compositing the exported PNG over a different background than the studio's own. Fixed with a
separate alpha blend factor pair — `Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha` — the
standard "over" formula, which keeps alpha at 1 wherever the destination is already opaque. This is
what makes the card frame (with the background on) always export fully opaque regardless of edge
antialiasing.

**2026-07-27: "Enable background" toggle — transparent-with-card-frame capture.** ⚠ *Superseded by the
2026-07-30 background removal below — this toggle and the background quad it gated no longer exist;
kept for the alpha-export reasoning, which still applies.* Previously, with
the card frame enabled, the opaque background quad filled the entire frame, so the "Transparent
background" capture toggle was effectively overridden (you always got the card's own gradient
background, never true transparency). `StudioCardFrame.BackgroundEnabled` (EditorPrefs-backed,
default **true** = identical to the old always-on behavior) gates `_bg.enabled` — same pattern as
the existing `SideMask` toggle on the mask quad. The background quad is the *only* opaque,
ZWrite-On layer in the whole card-frame stack (see the "over" alpha-blend fix above — it's what
forces every other layer's antialiased edges to export as fully opaque too); switch it off and
`OutfitCapture` additionally clears the camera to alpha 0 whenever `StudioCardFrame.Enabled &&
!StudioCardFrame.BackgroundEnabled` (independent of the separate "Transparent background" checkbox,
which still works for the no-card-frame case) — so the area outside the card comes out transparent
in the PNG while the card panel and avatar (still opaque themselves) are unaffected. See §8 for the
`RecoverAdditiveAlpha` post-pass this exposed a need for (bloom/glow near the card's edges wasn't
writing alpha, so it vanished over the new transparent region).

**2026-07-27: "Use Decentraland Background" toggle — animated purple loading-screen pattern.**
Ported the animated background from `unity-explorer`'s Welcome/loading screens (shader
`Custom/AnimatedBackgroundMovingTexture` in `TileableTexture.shader`, tuned via `BackgroundLoading.mat`)
directly into `StudioCardFrame.shader`'s existing background mode (`_Mode 0`/`3`), rather than adding
a 6th quad — the background is always a single opaque, ZWrite-On quad, so the two looks (gradient vs.
DCL pattern) are just alternate branches inside the same `frag()`, selected by a new `_UseDclBg`
toggle property:
- **Vignette:** `_DclInnerColor`→`_DclOuterColor` radial falloff (`_DclRadius`/`_DclSmoothness`)
  replaces the vertical `_ColorA`/`_ColorB` gradient. Same purple palette as the reference
  (`#BF00FF`→`#4D0080`).
- **Overlay pattern:** `DclBackgroundPattern.png` (copied from Explorer's `DCL_LogoPattern.png` — a
  tileable atlas of t-shirt/crown/headphones/banana icons) is sampled with UVs scrolling via
  `_Time.y * _DclOverlayDirection * _DclOverlaySpeed` (shader-driven, no C# ticking needed — same
  approach as the reference, which has no animation script either) and composited with a
  "luminosity blend" (`RgbToHsv`/`HsvToRgb` helpers): the overlay's own hue/value stay, but its
  saturation/hue get pulled toward the vignette's, so the pattern tints purple instead of showing raw
  white/gray.
- **Secondary glow:** a second radial highlight (`_DclGlowColor`/`_DclGlowCenter`/`_DclGlowRadius`),
  independent of the existing `_HighlightColor` glow (which is skipped while the DCL mode is active).
  The reference material's hotspot sits off-center (`_DclGlowCenter` (0.68, 0.5)), but since this
  background composites behind a centered avatar rather than filling an unrelated loading screen,
  `_DclGlowCenter` stays recentered to (0.5, 0.5) here so the glow lines up behind the subject —
  the one deliberate deviation left after the 2026-07-30 parity pass (below).
- Reuses the same opaque `return float4(col, 1.0)` exit as the gradient path for mode 0, and the same
  side-mask repaint logic for mode 3 — so **Mask avatar to card sides** repaints with the DCL pattern
  too when both toggles are on, staying seamless with the background quad exactly as it already did
  for the gradient.
- `StudioCardFrame.UseDclBackground` (EditorPrefs-backed, default **false**) pushes `_UseDclBg` and
  the (lazily `AssetDatabase`-loaded) pattern texture onto the bg/mask materials in `PushParams()`;
  all the DCL tuning constants (colors, glow, tiling/speed) are left at the shader Properties-block
  defaults ported 1:1 from `BackgroundLoading.mat` rather than exposed as new controls — this is a
  fixed "look", not a tunable gradient like the existing background colors.
- Animation requires the Scene view's "Animate Materials" toggle when previewing outside Play mode
  (standard Unity behavior for any `_Time`-driven shader); it always animates in Play mode and in
  Captures, since `_Time` advances there regardless.
- **Retuned same day** (Mauricio: bright center should occupy more space, dark should read as a
  corner vignette, not dominate): `_DclRadius` 0.167→0.42 and `_DclSmoothness` 0.25→0.55 (range
  widened to `Range(0.01,1)` to fit). At these values the full-bright disc now covers most of the
  frame and the falloff only reaches the outer color out past the UV corner distance (~0.707), so
  mid-edges stay mostly bright and only the corners darken — an actual vignette instead of the
  reference's small hot-spot-in-a-dark-field look. **Reverted 2026-07-30** (below) — the ask flipped
  to exact reference parity.

**2026-07-30: DCL background made pixel-identical to Explorer's.** Side-by-side (studio vs. the
Explorer welcome screen) the purple read far too bright and the vignette barely showed. Sampling the
comparison screenshot pinned it down: the *Explorer* half's frame corners are exactly `_OuterColor`
in gamma (77,0,128 @8-bit), i.e. the raw shader output with no tonemapping — and the studio half's
corners were (153,0,199), which reproduces the port's own math exactly. So this was never a color
space / post-processing gap (ACES would have lifted G off 0 on both halves, and it's 0 in both); it
was four concrete deviations, all now fixed:
1. `_DclRadius` 0.42 → **0.167** and `_DclSmoothness` 0.55 → **0.5** — reverts the 2026-07-27 retune
   above. Note 0.5 is the value in `BackgroundLoading.mat`; the original port's 0.25 had picked up
   `TileableTexture.shader`'s Properties-block default instead of the material's override (every
   other constant was taken from the material correctly).
2. `_DclGlowCenter` **stays at (0.5,0.5)**, not the material's (0.68,0.5). Briefly moved to the
   reference value during this pass and immediately put back (Mauricio) — the recentering is wanted:
   the hotspot belongs behind the avatar, not beside it. Only deliberate deviation that survives.
3. **Glow falloff was linearised.** The reference builds `float4 glow = _GlowColor * glowMask *
   _GlowStrength` and then adds `glow.rgb * glow.a`, so mask and strength each apply *twice*; the
   port applied each once. That turned a tight hotspot at effective strength 0.35 into a wide wash at
   0.59, washing out what vignette was left. Now replicated verbatim as `glow*glow` (see the shader
   comment — it looks like a redundant multiply and must not be "simplified" away).
4. **New `_DclUvScale`** (pushed from `PushParams()` as `BG_OVERSIZE`). The BG/side-mask quad is 4%
   bigger than the frustum to hide edge slivers, so its raw UV 0..1 spans more than the visible
   frame — the whole DCL look was evaluated 4% off-scale, and the visible corners sat at UV radius
   0.68 instead of 0.707 (worth ~7/255 on its own). `DclBackground()` now scales UV about the centre
   to undo it; the gradient path and the mode-3 card-rect mask keep the raw UV (that's the space
   `_MaskRect` is expressed in).

With these applied, a simulation of the fixed shader lands within 1–3/255 of the Explorer pixels
measured at seven background points (corner exact; residual is the animated pattern's phase). The
glow ellipse is the sole exception, and only because it's deliberately translated: its peak is the
same (~(200,0,255) @8-bit) but now sits at UV x 0.5 instead of 0.68, so the frame centre reads ~22/255
brighter than Explorer's and x=0.68 correspondingly darker. Everything outside that small ellipse is
unaffected by the move. The
palette, texture (byte-identical PNG, same sRGB/repeat import), tiling/speed/alpha, luminosity blend
and remaining glow constants were already 1:1 and were left alone.

**2026-07-30, later: background layer DELETED; the Decentraland paint moved onto the card.** Mauricio:
the frame should have no backdrop at all (black live, transparent on export) and the card itself should
carry the colours. So the whole background concept is gone — the quad, both toggles, and every colour
that fed them:

| Removed | Replaced by |
|---|---|
| `_Mode 0` Background quad + `_bg` renderer | nothing — outside the card is the camera's clear |
| **Enable background** toggle (`BackgroundEnabled`) | gone; there's no background to enable |
| **Use Decentraland Background** toggle (`UseDclBackground`) | gone; the DCL paint is unconditional |
| **Background** group: `BgTop`/`BgBottom`/`Glow`/`GlowHeight`/`GlowSize` (+ `_ColorA`/`_ColorB`/`_HighlightColor`/`_HighlightCenter`/`_HighlightSize`) | nothing |
| **Card** `CardTop`/`CardBottom` gradient fill | `DclInnerColor`/`DclOuterColor` — the card is now painted by `DclCardPaint()` |
| **Bottom fade** `Fade` colour (+ `_FadeColor`) | the fade samples the card's own paint at the same UV |
| `_DclUvScale` (added hours earlier for the oversized BG quad) | `_DclTileScale`, see below |

Modes were renumbered with mode 0 gone — **Card 0, Fade 1, SideMask 2, Border 3** (safe: `_Mode` is
only ever set from `StudioCardFrame.Create()`, never serialized). Four consequences worth knowing:
1. **The card writes depth now** (`ZWrite On` + `clip()` on its transparent pixels). It inherits that
   job from the background quad — without any ZWrite-On layer, the skybox draws after the opaque queue
   and paints over the card in any view that clears to Skybox (the Scene view). See the Card bullet
   above.
2. **The side mask erases instead of repainting**, which needed a second material-driven blend pair
   for alpha (`_SrcBlendA`/`_DstBlendA`). See the side-mask section above.
3. **Pattern scale had to be re-derived.** The reference tiles a fullscreen quad by the SCREEN aspect;
   the card's UV spans the card, so tiling is now `_DclOverlayTiling * _DclTileScale * (cardAspect, 1)`
   where `_DclTileScale` is the card's height as a fraction of the frame's (`1 - MarginTop -
   MarginBottom`, pushed from `PushParams()`). That keeps each icon the same **on-screen size** as in
   the reference at any margin setting — and since the scroll offset is in tile units, the same
   **on-screen speed** too. Using the screen aspect here would stretch the icons; dropping
   `_DclTileScale` would blow them up ~2.7× on a 0.66-aspect card.
4. **Captures always want alpha while the frame is on.** `OutfitCapture`'s gate collapsed from
   `Enabled && !BackgroundEnabled` to just `StudioCardFrame.Enabled`. `StudioCardFrame.SyncCameraClear()`
   additionally drives the live camera to a `SolidColor (0,0,0,0)` clear while the frame is enabled
   (saving/restoring the authored values), so the Game view matches the export instead of showing the
   scene camera's authored purple around the card. **Play mode only** — in edit mode the preview renders
   through the Scene view, which owns its own background, and writing to the scene camera there would
   dirty the scene.

**Same day, follow-up:** Mauricio hit a tall pose whose shoes hung below the card. The existing side
mask *did* clip the bottom, but only bundled with the sides. Split into two independent toggles
(`SideMask` / `BottomMask`, the latter defaulting **on**) driving one generalized rect — see the
clipping section above. "Both on" is bit-identical to the old single-toggle behaviour, so nothing that
was already tuned moves.

**Presets keep up:** `CardColorPreset` carries the card's whole paint — `cardInner`, `cardOuter`,
`border` and (added with the field) `pattern`, a direct `Texture2D` asset reference. Save writes all
four, apply pushes all four and rebuilds the body so the ColorFields and the Pattern ObjectField show
what landed. A preset with an empty `pattern` (one authored before the field existed, or a fresh asset
from the Create menu) applies the **bundled default** rather than keeping whatever is currently set —
a preset should fully determine the look, not half-inherit it. The three checked-in presets name
`DclBackgroundPattern` explicitly so they're complete. Margins/sizes/radius/widths and the toggles stay
untouched by design: a preset re-skins the current layout.

`CardColorPreset` went from **7 colours to 3** (`cardInner`, `cardOuter`, `border`) — Mauricio
pre-accepted breaking old presets. The three checked-in assets were migrated in place by mapping
`cardTop`→`cardInner` and `cardBottom`→`cardOuter` (the lighter/darker pair maps cleanly onto
centre/edge), keeping `border`; the four dropped fields are simply gone from the YAML. Any preset
asset elsewhere in the project will silently come back with default inner/outer and its old border.

### Lifecycle
Quads are `HideFlags.DontSave` (never serialized → no scene churn) and parented to the camera so they
track the view (incl. drag-rotate, which rotates the avatar only). Recreated after a domain reload
(`TryReattach` finds a surviving root by name, else rebuilds) and after the play-mode scene reload
(the poll re-parents to the new camera). Edit-mode preview and play mode both show the frame.

### Verification (needs the editor — not yet run)
1. Focus Unity → `Custom/StudioCardFrame` compiles, no CS errors in the new files.
2. Studio scene, edit mode: load an outfit, open **Card frame**, tick **Enable** → the rounded card
   appears behind the avatar within ~0.5 s, painted magenta-centre → dark-purple-edges with the icon
   pattern over it; **nothing** behind the card; head overflows the top; legs fade into the card at
   the bottom, with the fade's colour indistinguishable from the card's at the seam.
3. Tweak Inner/Outer colour, Pattern, Card Width, margins, radius, border, fade → live update. Every
   length is in **px** (§20): type a 24 px radius, then change Card Width and the top/bottom margins —
   it must still measure 24 px, and the readout under the margins must track the card's size (plus the
   outer border's extra footprint, width both sides / height bottom only). Changing margins must NOT
   change the pattern's icon size either (that's what `_DclTileScale` is for). Drag-rotate → the card
   stays put, the avatar rotates.
3b. **Change the capture Size, then press "Match Game view to capture size"** → the status line goes
   green and the card is *identical in shape*, just scaled: only the capture height should change any
   pixel value, never the width (§20 step 3). This is the one that used to break the card completely.
4. Enter play → the area outside the card goes black (`SyncCameraClear`), not the scene camera's
   purple. Pick a pose, **Capture Still** at e.g. 900×1350 → PNG shows the card + avatar opaque and
   everything around the card **transparent** (set the Game view to a 2:3 portrait aspect first).
5. Tick **Mask avatar to card sides** with a wide pose → arms/hands are erased at the card edge (and
   in the export that region is transparent, not black), head still overflows, and the border's outer
   ring still draws over the erased area. With **Mask avatar below card** on (the default) a tall pose's
   feet stop dead at the card's bottom edge; untick it and they hang out again. Check all four
   on/off combinations — in particular that with sides off + bottom on the cut is a clean straight
   line across the full frame width, and that no combination bites a crescent out of the card's own
   rounded corners (that's what the aspect/radius restatement protects).
6. Toggle Enable off → quads vanish, plain preview returns AND the camera's original clear
   flags/colour come back (check the Camera inspector in play mode). Prod safety: confirm the shader
   is absent from a WebGL build report and no diffs on any `URP_*`/renderer-data asset, `Main.unity`,
   or `OutfitStudio.unity` (the clear override is play-mode-only precisely so this holds).

### Possible v2s
Name/price/"+" text chrome (deferred — not a DCL concept; would be an extra quad or a captured text
layer); per-side avatar hard-clip via a stencil if a wide pose ever spills past the card; save/load
card presets alongside outfit presets; exposing more of the Decentraland paint (vignette radius,
pattern tiling/speed, glow) as card sliders if the fixed reference look ever needs art direction.

## 19. Avatar tab — body shape/colors relocated + curated face features (2026-07-23)

New top-level tab (`BuildAvatarPane`, alongside Avatar/Wearables/Emotes-Poses/Debug) consolidating
everything about the *body itself*, mirroring the marketplace's own avatar editor:

- **Body shape + colors**: moved here verbatim from the Outfit pane (no behavior change — still
  writes straight to `outfit`, so still shareable/saveable in presets, just relocated).
- **Face features** (new): eyes/eyebrows/mouth/hair/facial_hair, browsed as a tile grid
  (`BuildFaceTile`, same visual language as `WearableItemElement`) per `FACE_SLOTS`/
  `FACE_SLOT_LABELS`, filtered to the currently-selected `CurrentBodyShape()` (male/female-specific
  URN variants are mixed in `DEFAULT_FACE_URNS`; picking one without a representation for the active
  body shape would silently no-op at apply time, so those are excluded from the grid instead).

**Why this needed its own data path, not `CatalogService`:** these are **base-avatar (off-chain)**
wearables — the marketplace-api (`v2/catalog`) only serves collection items, so face features are
resolved via the **Catalyst entities endpoint** instead (`RunFaceSearch`, async void — same
fire-and-forget pattern `EditModeAvatarPreview.Apply` already uses for editor-only await chains).
`DEFAULT_FACE_URNS` is a curated per-slot URN list mirroring the same set the in-game avatar
Configurator ships with (`Assets/Scripts/Configurator/ConfiguratorController.cs`'s
`faceCategories`) — deliberately not a live catalog browse, since there's no marketplace-style
search/pagination available for this data source.

**Local-only, deliberately never shared.** Face-feature picks live in `_previewFaceUrns` (a plain
`Dictionary<string, string>` on the window, not on `outfit`) and are merged in only at
preview/capture time via `BuildPreviewOutfit()` — `outfit` plus the local overrides, with a
conflicting real outfit item in the same slot dropped (same one-item-per-slot rule
`OnItemClicked` already applies to ordinary equips). This means **a share code or saved preset never
carries face-feature picks** — intentional, since these are meant as local preview/beauty-shot
overrides (e.g. "how would this outfit look on a different face") rather than part of the outfit
being authored.

> **Superseded by §21 (2026-08-03).** `_previewFaceUrns` and `BuildPreviewOutfit()` are gone — face
> features are now ordinary `outfit.urns` entries and *do* travel in presets and share codes. The
> paragraph above is kept for the rationale it records, not as current behaviour.

## 20. Pixel-exact authoring (2026-07-30, step 1 of the px workflow)

The tool's next audience is marketing artists coming from Figma/Photoshop, who think in pixels and
expect what they frame to be what they export. Two separate things have to hold for that, and only one
of them was already true:

**1. The export is already exact.** `OutfitCapture` renders through the Recorder's `CameraInputSettings`
with `OutputWidth`/`OutputHeight` set to the capture Size, and forces `camera.aspect = w/h` +
`StudioCardFrame.RelayoutFor(camera)` first. So the PNG is exactly Size × Size pixels whatever the Game
view is doing. Nothing to fix.

**2. The framing was not.** The card frame lays itself out from `camera.aspect`, so a Game view at
1920×1080 and a capture at 1200×800 produce *different framing* — the artist tunes margins against one
composition and exports another. Fixed in `StudioGameViewSize`: **"Match Game view to capture size"**
(under the Size fields in the Capture pane) adds — or reuses — a **Fixed Resolution** entry of exactly
that size in the Game view's size list and selects it on every open Game view. A polled status line
underneath reads the live size back (`Handles.GetMainGameViewSize()`, public API) and warns whenever the
two drift apart; polled because the artist can change the Game view's own dropdown at any time and
there's no notification for it.

Setting the size is **internal API** (`UnityEditor.GameViewSizes` / `GameViewSize` /
`GameView.selectedSizeIndex` by reflection) — Unity has never exposed the size list. The recipe is
long-stable and verified on 6000.4.0f1, but every step is null-checked and the whole call is wrapped:
if a future Unity renames something, the artist gets a Console warning telling them to add the Fixed
Resolution entry by hand, never an exception. Entries we create are labelled `Outfit Studio <W>x<H>` so
they're recognisable in what is a shared, project-wide list, and so repeat presses reuse rather than
pile up duplicates.

### What "no resizing" can and can't mean
Worth being precise, because the two get conflated:
- **Render size == capture size** (framing/composition WYSIWYG) — guaranteed by the above.
- **1 game pixel == 1 monitor pixel** (so you can judge a 2 px border by eye) — *not* guaranteed. If the
  Game view panel is smaller than the resolution, Unity scales the displayed image down to fit; it still
  *renders* at the fixed resolution, so the capture is unaffected, but you're looking at a shrunk
  preview. And at 125%/150% OS display scaling, Unity's 1× maps one texture pixel to one *logical*
  point, i.e. 1.25/1.5 physical pixels. So true 1:1 needs a panel at least as large as the capture, at
  100% OS scaling — impossible at the old 2048×2048 default on a 1080p monitor. The Scale slider at the
  top of the Game view shows the current factor.

### Step 2: every card length is now a pixel field
Card knobs are still **stored as fractions** — that's not a compromise, it's the point: the same
settings have to export identically at 1200×800 and 2400×1600, just at twice the pixels, and the shader
math has to stay resolution-independent. What changed is that the *fields* show and accept pixels of the
current capture size, so an artist types Figma numbers. `CardPxSlider` takes a `toPx`/`fromPx` pair;
nothing about the render path, the EditorPrefs values or `CardColorPreset` changed.

**Every knob is stored relative to the CAPTURE, never to the card** — that's the key decision, and it's
what makes "a 24 px radius is 24 px" true by construction (Mauricio, and it's how Figma/CSS behave). The
window's conversion is then a plain multiply with nothing card-dependent in it:

| Knob | Stored fraction of | → px |
|---|---|---|
| Card Width | capture **height** (see below) | `× FrameHPx` |
| Margin Top / Bottom | capture height | `× FrameHPx` |
| Corner Radius, Inner/Outer Border Width | capture **height** | `× FrameHPx` |
| Fade Height | capture height | `× FrameHPx` |
| Fade Softness | — | stays a 0–1 ratio: it's the share of the fade that ramps, not a length |

The shader still needs card-relative numbers, so **`PushParams()` restates them**, and that's the only
place the two unit systems meet:
- `RoundedBoxSDF` normalizes so the card's **half**-height is 1, so radius/border widths convert as
  `frameHFraction × 2 / cardHFrac`. The ×2 is the classic thing to get wrong — miss it and every radius
  is half what it should be.
- The fade ramps over the card's own `uv.y`, so it's `frameHFraction / cardHFrac` (no doubling),
  `Clamp01`'d at the whole card.
- **Clamping happens in frame-height terms, before the conversion**, so every quad derives from the same
  clamped physical size. That matters because the card and the side mask must round their corners
  identically or the mask's edge stops landing on the card's AA edge — they now both call the same
  `ToSdf(radiusFH, thatRect'sHeight)`, which also deleted the old `× (cardH/effH)` fudge in the mask.
  Radius clamps at SDF 0.5 (`RoundedBoxSDF` clamps to the box anyway), border widths at
  `MAX_BORDER_WIDTH` (past it the outer ring overflows the border quad, whose oversize is sized for
  exactly that maximum). Neither is reachable at sane margins — they only bite on a squashed sliver.

Verified numerically: the stock defaults reproduce the previous card-relative values *exactly*
(radius → SDF 0.08, inner border → 0.008, fade end → 0.2), a 24 px radius stays 24.00 px across a taller
card / a squat card / a horizontally narrowed card, and doubling the capture height doubles it to 48 px
(proportional, which is what you want when exporting the same design at 2×).

**The four keys changed unit, so they got new names** (`…RadiusFrameH`, `…InnerBorderWidthFrameH`,
`…OuterBorderWidthFrameH`, `…FadeHeightFrameH`). A stale value under an old key would be silently
misread as the new unit and quietly change someone's tuned card; abandoning the old keys means everyone
lands on the restated defaults instead. Defaults are the old values converted at the default margins
(`cardHFrac` 0.83): radius `0.08 × 0.83/2 = 0.0332`, inner border `0.00332`, fade `0.2 × 0.83 = 0.166`.

### Step 3: the card no longer changes shape with the capture aspect
Mauricio, straight after step 2: *"when i change the res, the card breaks completely."* It did, and it was
one line in `Layout()` — the card's width came off the frame **width** (`cw = w * (1 - 2*MarginX)`) while
everything else came off the frame **height**. Since the camera frames the avatar by its *vertical* fov,
the avatar's on-screen size only ever tracks the frame height, so a width-relative card changed both its
own shape *and* its size relative to the avatar every time the aspect moved. A resolution change reshaped
the composition instead of scaling it.

Fixed by replacing the **Margin Sides** knob with **Card Width**, stored as a fraction of the frame
**height** (`cw = h * CardWidth`), card always horizontally centred. With the height already
height-relative, the card's aspect is now fixed by its own two knobs and the capture's aspect can't touch
it. The frame aspect survives in exactly one place — `_frameAspect`, stashed by `Layout()` — because the
side-mask rect is expressed in viewport fractions *per axis*, so the card's width has to be converted
back into a width fraction there. That conversion also collapsed the mask's aspect term from
`cardAspect × (effW/effH) × (cardHFrac/cardW)` to just `_frameAspect × (effW/effH)`; the two are
algebraically identical, the old form only looked card-dependent.

Verified by simulating `Layout` + `PushParams` across resolutions: at a fixed capture height of 800, the
card comes out **440 × 664 px at aspect 0.6627 with a 24.00 px radius** at 600×800, 800×800, 1200×800 and
2000×800 alike; doubling the height to 1600 doubles all of it (880 × 1328, 48 px radius) and 1080×1080
lands in between. Nothing depends on the width any more.

`MarginX`'s key is abandoned rather than reinterpreted (`OutfitStudio.Card.WidthFrameH` is new), since
the meaning changed and not just the unit.

**UI mechanics.** `maxPx` is a *callback*, not a value, because ranges move with the capture size (and,
for radius/fade, with the card — a range may track the card even though the value doesn't: past ~a
quarter of the card's height a "radius" stops meaning anything). `SyncCardPxFields` (polled on the card
body, registered once outside `BuildCardBody` so rebuilds don't stack up pollers) re-reads every field,
since the same fraction is a different pixel count after a capture-size change and there's no event for
it. It skips whatever currently has focus, so it can't fight a drag or overwrite a half-typed number, and
it writes with `SetValueWithoutNotify`, so its display rounding (2 dp) can never feed back into the
model.

**The `Card is W × H px of a W × H capture` readout** under the margins reports the card's **fill** rect.
Mauricio asked whether the outer border should count — it should: that ring is drawn at
`dist ∈ (0, _OuterBorderWidth)`, i.e. *outside* the card edge, so it enlarges the footprint even though
the fill rect doesn't move. Once it's non-zero the readout appends the painted size, **asymmetrically**:
the ring fades out over the top 12% (`_BorderTopFade`, so the head can still overflow), so it adds width
on *both* sides but height only at the *bottom* — `W + 2×outer` by `H + outer`. It reports
`EffectiveOuterBorderWidth`, i.e. post-clamp, so it can never claim a border wider than what's drawn.
The inner ring is deliberately absent from the readout: it's painted inside the card, so it changes
nothing about the footprint.

While adding that, `CardHeightFraction` and the three `Effective*` clamps moved onto `StudioCardFrame` as
properties — `1 - MarginTop - MarginBottom` and the clamp formulas each had two or three copies across
`PushParams`, the window's `CardHPx` and now the readout, which is exactly the kind of duplication that
drifts. `PushParams` reads the same properties, so there's one definition of each.

## 21. Face features are part of the outfit (2026-08-03)

Reverses the §19 decision: eyes/eyebrows/mouth/hair/facial_hair picked on the Avatar tab are now
equipped into `outfit.urns` like any other wearable, so **presets and share codes carry them**.
Mauricio's framing: what you select on the Avatar tab *is* the look, so saving an outfit that
silently drops the face it was authored with is the surprising behaviour. `_previewFaceUrns`,
`BuildPreviewOutfit()` and the "Preview only" notice are deleted; `Apply()` reads `outfit` directly
again.

**Nothing changed in the share-code format.** Face features are ordinary wearables and the code
already carries arbitrary `&urn=`, so they ride along as plain URN entries — no new query param, no
renderer-side change, and old codes/presets (which simply have no face URNs) load exactly as before.
Skin/hair/eye colours and body shape needed no work at all: they have been `OutfitDefinition` fields
emitted by `ToShareCode()` since §19 relocated them.

**The part that actually needed building — `RegisterCatalystEntities()`.** `_previewFaceUrns` wasn't
just a storage choice, it was standing in for `_knownItems` in two places that key off it:

1. **One-per-slot dedup** (`OnItemClicked`/`OnFaceFeatureClicked`) resolves an equipped URN's slot via
   `_knownItems[urn].Slot`. Base avatars are **off-chain**, which `HydrateKnownItems` used to skip
   outright because marketplace-api can't resolve them (§19) — so a base hair wouldn't evict a
   marketplace hair and you'd carry two, leaving it to the renderer's last-in-list-wins backstop.
2. **`FilterForBodyShape`** also keys on `_knownItems`, and unknown URNs pass through unchecked — and
   per §"Slot semantics" a URN with no representation for the active body shape makes
   `GLTFLoader.LoadModel` **throw and break the whole load**. Previously unreachable (the grid only
   ever shows URNs valid for the current shape); now reachable, because a preset authored on Male can
   be loaded on Female.

`RunFaceSearch` already receives `EntityDefinition[]` from the Catalyst carrying `URN`, `Category`
(= the wearable category, from `metadata.data.category`), `Thumbnail` and `HasRepresentation()`, so
those get registered as **synthetic `CatalogItem`s** — `bodyShapes` spelled `"BaseMale"`/`"BaseFemale"`
because that's what `FilterForBodyShape` string-compares against. That one addition makes dedup, the
body-shape guard *and* the Outfit pane's rows (name/thumbnail/slot/✕) work for face items with no
special-casing anywhere else.

**`HydrateKnownItems` now resolves off-chain URNs instead of skipping them**, split across both
sources since neither covers the other: marketplace-api for collection items, Catalyst
(`HydrateOffChainItems`, `async void` like `RunFaceSearch`) for base avatars. Without this a loaded
preset would only resolve face items in whichever *one* category the Avatar tab happened to have
browsed this session — the other four would show as `[?] eyes_00` rows and skip both guards above.

Smaller consequences:

- `RefreshFaceGrid`'s selected-tile highlight reads `EquippedUrnForSlot(slot)` out of `outfit.urns`
  (`LastOrDefault`, matching last-in-list-wins) instead of the dict.
- `OnFaceFeatureClicked` takes the slot **from the grid that built the tile**, not `_faceCategory`, so
  a click can't land in the wrong slot if the category changed while thumbnails were still loading.
- "Clear selection" removes the slot's URN from `outfit.urns`, and reports when there's nothing to
  clear rather than silently doing nothing.
- `LoadOutfit` no longer clears face picks — it refreshes the grid so a loaded preset shows its own
  face selected, then `HydrateKnownItems` refreshes again once the async Catalyst lookup lands.
- Body-shape mismatch is **not** auto-corrected: flipping to Female with male-only eyes equipped
  keeps them in `outfit.urns` and lets `FilterForBodyShape` skip them with its existing status
  warning. Same as every other wearable — the studio never silently mutates the outfit on a shape
  change.

**Not editor-verified** (no Unity run in the session that wrote it).

## 22. Single-Item mode — one isolated wearable (2026-08-03)

> **The Subject switch described in this section is gone as of 2026-08-06.** Isolation is now a per-row
> action on the outfit list — read "Isolate from the outfit row" below first; it supersedes the opening
> paragraphs here, "Data model"'s separation rationale, and all of "Single-Item mode persists nothing".
> Everything about *why the item loads onto the skeleton* and how `StudioItemCamera` frames it is unchanged.

A **Subject** switch at the top of the outfit pane: **Avatar** or **Single Item**. Single Item shoots one
wearable with no body around it, for item-shop card sheets composed later in Photoshop (Mauricio's
reference: a Fortnite shop row, four items each centred in its own rounded card).

Everything else in the pane is **shared, not duplicated** — shader, card frame, pose, presets, capture,
px workflow, share code. Only the Outfit section swaps for an Item section. That's the whole reason the
switch lives in the right pane instead of being a fifth browser tab or a second window.

### Isolate from the outfit row — the Subject switch is gone (2026-08-06)

An artist using the tool asked for this directly: he didn't want Single Item to be a separate thing from
the Avatar part. He wants to **press a button on a wearable in the outfit list** to isolate it for a
screenshot, keep the Framing options while it's isolated, and have Framing go away when he's back on the
full avatar — "maybe its easier if everything its in the same page".

**The reframe that made it cheap.** `soloItem` stops being *a mode with its own subject* and becomes *a
view flag over the one outfit*, with an invariant:

> whenever `outfit.soloItem` is true, `soloUrn` is an entry of `urns`, or `soloBase64` is an entry of
> `base64Items`.

Because `soloUrn`/`soloBase64` keep their type and their meaning-as-a-pointer, **the entire render path is
untouched**: `EffectiveUrns()`/`EffectiveBase64Items()`/`EffectiveForceRender()`, `Apply()`'s
`config.HideBodyShape`, `EditModeAvatarPreview`'s body hide, `OutfitEntityResolver`, `StudioItemCamera`,
renderer touch point #8. All of the work is in the window, plus one honesty fix in `ToShareCode()`.

**The pointer stays a string, deliberately.** An index into `urns` would force an edit to `EffectiveUrns()`
— the one substitution point worth leaving alone — would need a discriminator to address `base64Items` as
well, and would silently point at a different item after any removal.

**What the list looks like.** `RefreshSlots` gained three marks, all reusing the vocabulary already on the
row: the ◉ button goes green + bold exactly like the `F` force-render toggle beside it, the isolated row
gets a 2px green left accent, and **every other item row drops to 45% opacity** — those rows genuinely
aren't rendering, and saying so on the row is what keeps "where did my outfit go" from being a support
question. Above the list, an isolation banner names the item (`Isolating [upper_body] Cyber Jacket — only
this item renders; the rest of the outfit is kept but hidden.`) and carries a **Show full avatar** button,
styled like the hide-override warning below it for the same reason. So there are two ways back: the banner,
or clicking the same row's ◉ again.

**Deleted, not moved:** `BuildSubjectSwitch`, the `Subject` header, `SetSubject`, the `_outfitSection`
wrapper (nothing hides it now), and — the one worth arguing — the whole **Item** header + `_itemRow` +
`RefreshItemRow`. That row duplicated a row an inch above it with a *worse* identity display (no hiding
badge, no force toggle), and its `✕` meant "clear the pointer, keep the item equipped" while the row's `✕`
meant "unequip": two identical glyphs, two semantics, adjacent on screen. That divergence is the confusion
this merge existed to remove. `OutfitDefinition.HasSoloItem` went too — the state it described (mode on,
nothing picked) is now unreachable, since isolation can only be entered from a row that already exists.

**Presets and Share code are visible again.** The 2026-08-04 rationale below was sound *while solo replaced
the outfit list*; once the list is on screen and isolation is a view of it, two sections vanishing because
you pressed a row button is just jarring. They act on the whole outfit, always:

- **Preset — no code change.** `CloneForPreset()` already defaults every solo field, so a preset saves the
  list you can see and loads as a full avatar. Under the new model that reads *better* than before.
- **Share code — a required fix.** `ToShareCode()` emitted `EffectiveUrns()`, i.e. **one urn** while
  isolated. Defensible when the box was hidden; with Copy sitting two sections under a visible six-row
  outfit, it would silently publish a one-item outfit. Both loops now read the raw `urns`/`base64Items`.
  Nothing is lost — `FromShareCode` can't express isolation, so no round trip ever existed.
  `HasStudioOnlyState` dropped its `|| soloItem` term (it had zero callers either way).

**Browser picks now follow one rule, with no hidden modality.** The three equip sites (`OnItemClicked`,
`OnFaceFeatureClicked`, `EquipDraft`) each had an `if (outfit.soloItem) { SetSoloItem(...); return; }` branch
that replaced the isolated item *without equipping it*. All three now equip exactly as normal and then, if
a row is isolated, **re-point isolation at the item just equipped**. Reasons: the isolated item has to *be*
a row for the button to live on it; a pick that means two different things depending on hidden state is the
modality being removed; and without the re-point, clicking a hat while isolating a jacket equips something
that doesn't render and the browser looks broken.

The accepted cost, stated plainly: **shooting a sheet of items accumulates a row per slot.** It's bounded by
the existing one-per-slot displacement, fully visible, one `✕` to undo, and nothing auto-persists. The real
answer for a 20-item sheet is open thread 3 (batch export), which wants the items enumerable from a list —
i.e. exactly this model.

**One funnel, and a re-entrancy trap.** `ReconcileSoloSelection()` drops isolation when the isolated entry
is no longer equipped, and is called from **exactly one place**: the top of `RefreshSlots`. Every mutation of
`urns`/`base64Items` already funnels through there (both `✕` handlers, all three equip sites, `LoadOutfit`,
both hydration callbacks, `OnHidingReportChanged`), and each already schedules an apply. It deliberately does
**not** call `RefreshIsolation` — that calls `RefreshSlots`, and a nested `_slotsContainer.Clear()` mid-loop
would garble the list; on a true return `RefreshSlots` hides the framing section directly and says so in the
status line. This is also what handles stale serialized state from before the change: a `soloUrn` that isn't
equipped drops on the first list rebuild rather than needing serialization work.

**Two bugs this newly exposed, both fixed:**

1. **The camera stayed parked after loading a preset while isolated.** Presets are reachable in this state
   now, and `CloneForPreset` guarantees the loaded instance has `soloItem = false` — so the reconcile sees
   nothing to reconcile and never releases the brain. That's §22's original "the camera is all off", by a new
   route. `LoadOutfit` captures `wasIsolated` before the swap and calls `StudioItemCamera.Release()`.
2. **The framing sliders went stale.** Exactly the bug documented under "Single-Item mode persists nothing"
   below — it was closed by *hiding* the section on load, and hiding is what this change removes. Now
   `_syncFramingFields` (assigned at the end of `BuildFramingSection`, invoked from `RefreshIsolation`)
   pushes `outfit.solo*` back onto the five controls with `SetValueWithoutNotify`, and retitles the header
   `Framing — [slot] Name` so the section identifies its subject once the list has scrolled off.

**Verified by compiling, unusually for this file.** Unity ships a runnable Roslyn (`Editor/Data/
NetCoreRuntime/dotnet.exe` + `DotNetSdkRoslyn/csc.dll`), and with no `.asmdef` anywhere the two assemblies
are just "everything under an `Editor/` folder" and "everything else". Compiling both from source against
`Library/ScriptAssemblies/*.dll` + `Editor/Data/Managed/UnityEngine/*.dll` gives **zero errors** in
`OutfitDefinition.cs` and `OutfitStudioWindow.cs`. Two gotchas if repeating it: quote every `-r:` path (the
Unity install lives under `Program Files`), and reference the **module** DLLs only — adding
`Managed/UnityEngine.dll`/`UnityEditor.dll` alongside them produces hundreds of CS0433 duplicate-type
errors, and adding an `mscorlib.dll` on top of `netstandard.dll` produces thousands. The 24 residual CS0012
`mscorlib` errors in `OutfitCapture.cs` are a facade artifact of that ad-hoc reference set, not real. **This
still isn't an editor pass** — it proves the code compiles, not that the UI behaves.

### Why it loads onto the skeleton instead of standing alone

There are two ways to render one wearable, and Mauricio's "we will need the possibility to pose them,
so for example upperbody meshes are not in T pose" picks the winner.

`WearableLoader` already renders a wearable standalone — that's the marketplace wearable card
(`PreviewController.LoadForMarketplace` → `WearableRoot` at `(5,0,0)` + `MarketplaceWearableCamera` +
`GameObjectUtils.CenterAndFit`). It works because **every DCL wearable GLB ships its own copy of the
62-joint avatar armature**: glTFast instantiates those joints and binds the `SkinnedMeshRenderer` to
them, so `AvatarUtils.SetupWearable`'s optional `avatarRootBone`/`avatarBones` args can be omitted and
the remap at `AvatarUtils.cs:189-197` is simply skipped. But that armature is in its **authored rest
pose**, and nothing can move it: `GLTFLoader.LoadModel` imports every body and wearable with
`AnimationMethod.None` (`GLTFLoader.cs:25-31`), so wearable-embedded clips are discarded outright, and
`WearableLoader` never touches `SpringBonesDriver` or `EmoteAnimationController`. **That path *is* the
A-pose problem.**

So Single-Item mode loads the item through the **normal `AvatarLoader` path and hides the body**. The
fact that makes this safe: in the scene hierarchy the skeleton (`Avatar_Model_Idle` → `Armature`) is a
**sibling** of the loaded GLB roots under `AvatarRoot`, not a parent —

```
AvatarRoot            [AvatarLoader, Animation, EmoteAnimationController, SpringBonesDriver]
├─ Avatar_Model_Idle  → Armature → Avatar_Hips … 62 joints   ← avatarBones, the live skeleton
├─ "body_shape"       ← runtime GLB root, named by category
└─ "upper_body"       ← runtime GLB root
```

— so deactivating any GLB root cannot break skinning for the others. `SetupWearable` remaps the item's
bones onto the live skeleton (`AvatarUtils.cs:307-322`, name-keyed, bind poses untouched), which means
poses, emotes, spring bones, drag-rotate, snap-rotate and the turntable all apply to the item **with no
new code**. Single-Item mode is therefore not a subsystem: it's *an outfit with one item, the body
suppressed, and the camera moved in close*.

### Renderer touch point #8 — `PreviewConfiguration.HideBodyShape`

Additive and default-off, copying the `DisableFace` pattern line for line. `PreviewController`'s
`LoadForBuilder`, immediately after `LoadAvatar` and next to the existing `DisableFace` block:

```csharp
if (PreviewConfiguration.Instance.HideBodyShape)
    avatarLoader.TryHideCategory(WearableCategories.Categories.BODY_SHAPE, true);
```

`TryHideCategory` deactivates the whole body GLB root, and the skeleton is outside it. Two details:
it **must** be re-applied on every load because `LoadAvatar` reactivates every model root it owns
(`AvatarLoader.cs:169`); and `AvatarLoader._hiddenCategories` turns out to be **write-only dead state**
(added to and removed from, never read), so toggling back needs no cleanup. Deliberately **no
query-string parameter** in `RecreateFrom` — it's an editor-tool concern, so deployed behaviour is
byte-identical.

Edit mode mirrors it in `EditModeAvatarPreview` (`bodyGO.SetActive(false)` when `soloItem`). Edit mode
still samples Idle at t=0 with no emote playback, so the status line says so: posing and capture are
play-mode only, same constraint avatar mode already has.

### Data model, and the one substitution point

`OutfitDefinition` gains `soloItem` / `soloUrn` / `soloBase64` / `soloPadding`. `soloUrn` is kept
**separate from `urns`** so flipping subject is lossless — the outfit being authored survives a detour
into item shots. *(2026-08-06: this rationale is superseded. `soloUrn` is still a separate field, but it now
**points at an entry of `urns`** rather than holding a pick the outfit never saw — see "Isolate from the
outfit row". Nothing is lossless-vs-lossy about it any more: the outfit is never substituted, only viewed.)*
`soloPadding` lives on the outfit rather than in EditorPrefs because it's genuinely
per-item (a long staff and an earring want different margins), so a preset carries it. *(The framing field
is `soloZoomPct` now, and no preset carries any of it — see "Single-Item mode persists nothing" below. It
still lives on the outfit rather than in EditorPrefs, which is now just where the window keeps it.)*

The substitution happens in **two accessors**, not at every call site: `EffectiveUrns()` and
`EffectiveBase64Items()` return the isolated item in Single-Item mode and the whole outfit otherwise.
Routing `OutfitEntityResolver` through them is what makes the edit-mode preview **and** the hiding
report follow the mode without either knowing it exists. `EffectiveForceRender()` also returns every
category in Single-Item mode: with one wearable equipped there's nothing legitimate for a hide to
suppress, and an item whose own category is implicitly hidden (a skin, a helmet that hides hair) would
otherwise render as nothing at all. *(2026-08-06: "with one wearable equipped" is now "with one wearable
**loaded**" — the rest of the outfit stays equipped and is filtered out by `EffectiveUrns`, not hidden. The
force-everything conclusion is unchanged.)*

`ToShareCode()` emits `EffectiveUrns()`, so a shared code shows the same wearable — but there is no
hide-body parameter, so it loads on a full avatar. *(2026-08-04: this is no longer reachable — the Share
code section is hidden in Single-Item mode, see below.)* *(2026-08-06: reversed again, and this time fixed
properly — the section is visible and `ToShareCode` emits the raw `urns`/`base64Items`, so a code always
means "this outfit". See "Isolate from the outfit row".)*

### Switching subject hands the camera back (2026-08-04)

*(2026-08-06: retitled in spirit — it's entering/leaving **isolation** now, and `ClearIsolation` is where the
`Release()` call lives. Every word of the reasoning below still holds, and the failure it describes reappeared
by a second route — loading a preset while isolated — which is fixed in "Isolate from the outfit row".)*

Mauricio: *"when i go from single item to avatar, the camera is all off."* Cause: `FrameItem` disables
`CinemachineBrain` and writes `camera.transform` directly, and nothing re-enabled it on a subject switch —
so the avatar was being shot from the item's framing, at the item's distance.

`SetSubject` now calls `StudioItemCamera.Release()` when leaving Single-Item mode. **Only that one direction
needed anything**: `->Single-Item` already re-frames, because `SetSubject` ends in `ScheduleApply` which ends
in `ScheduleFrameItem`.

Chosen over saving a camera per mode, or running two cameras, because **neither mode's camera is authored —
both regenerate from an authoritative source**. Avatar framing is `builderCamera`'s authored shot (the studio
always runs builder mode), which re-enabling the brain restores exactly; item framing is derived from the
item's bounds on demand. A saved transform would only preserve a hand-flown position, and would itself go
stale the moment the item or outfit changed — a second source of "the camera is off" — besides fighting
auto-frame. Note the item's chosen *angle* survives regardless: drag/snap/turntable rotate the **rig**, not
the camera (see `StudioItemCamera`'s class comment).

**Two cameras would be the actively bad option**, and §22 already has the evidence — see "A camera worry
that turned out not to exist": `OutfitCapture` resolves via `Camera.main`, Recorder via
`ImageSource.MainCamera`, and the card frame prefers `PreviewCamera` filtered on `isActiveAndEnabled`. Those
agree today *only* because the scene's second `MainCamera`-tagged camera sits under an inactive root. A
second live camera makes "which camera produced this still" a real question again, and the card frame could
size itself off one camera's `fieldOfView` while another renders the shot.

If a hand-flown avatar angle ever needs to survive a round trip, the upgrade is to remember
`(position, rotation, brainEnabled)` per mode and restore it *only* when the brain was disabled by
`StudioFlyCamera` — i.e. when the artist actually flew somewhere deliberately. Deferred as unneeded.

### Single-Item mode persists nothing (2026-08-04)

> **Half of this is superseded (2026-08-06).** The *hiding* is gone — Presets and Share code are visible while
> a wearable is isolated, because the outfit list is visible too. What survives, and is still the reason
> things are shaped this way: `CloneForPreset()` (still defaults every solo field, so presets carry no
> isolation), and the stale-framing-slider bug below — which was closed by hiding the section and is now
> closed properly by `_syncFramingFields`. See "Isolate from the outfit row".

**`RefreshSubject` hides the Presets and Share code sections outright while Single-Item is the subject**,
alongside the Outfit/Item swap it already did. Mauricio: *"thats all for the avatar outfits which [are] the
main and most important part of the tool, when an artist go to the Single Item tab, its just for a specific
temporary work there and thats it."* So the mode is scratch space on the way to a PNG, and the two sections
that persist or publish an avatar *look* don't belong in it.

This replaced a half-working round-trip rather than a working one, which is the argument for it:

- A share code **cannot** express the mode (no hide-body parameter), so the button published something
  other than what was on screen. There was a warning label saying so; a section you must be warned not to
  use is better hidden.
- A preset **could** carry the solo fields, and `LoadOutfit` deliberately followed the loaded subject — but
  the Item section is built once and `RefreshSubject` only toggles `display`, so **the Framing sliders and
  toggles kept showing the previous values after a load** while `outfit` held the preset's. Touching any one
  of them then wrote the displayed value back, silently discarding what the preset said. This was §22's
  never-exercised "preset round-trip of the solo fields", and it was broken.
- Saving goes through the new **`OutfitDefinition.CloneForPreset()`** — `Clone()` with every solo field
  reset to its default (taken from a fresh instance, not repeated literals). Without it a preset saved from
  the *avatar* tab still carried whichever item sat in the solo slot plus its framing, invisibly, and
  loading it would stomp another artist's scratch work. `Clone()` itself stays faithful; presets are its
  only caller, but a lossy `Clone` would be a trap.
- Consequence worth knowing: **loading a preset resets the solo item and its framing to defaults.** That
  follows from "Single Item is temporary" and is the intended behaviour, not an oversight.
- `RefreshSubject()` is now called at the **end** of the pane build. Presets and Share code are built far
  below the Outfit/Item sections, so the old call site left them visible on a window that opened straight
  into Single-Item mode.

### `StudioItemCamera` — move the camera, never the rig

New `Editor/StudioItemCamera.cs`. Measures the item's bounds, then places the camera along its own current
forward so the item fills the target rect, aimed at the centre.

**Settled behaviour and defaults (read this; the bullets below are the reasoning and, in two cases,
superseded history).**

| knob | default | meaning |
|---|---|---|
| fit target | **frame** | `soloFitToCard` switches to the card rect |
| `soloZoomPct` | **100** | UI "Zoom Frame (%)", slider 25–250. 100 = item exactly touches the rect on the bound axis; **over 100 overspills and crops** |
| `soloOffsetYPx` | **70** | UI "Vertical Offset (px)", **positive = DOWN** (image-editor convention) |
| `soloOffsetXPx` | 0 | UI "Horizontal Offset (px)", **positive = RIGHT** |
| `soloFitGarmentOnly` | off | on = ignore bare skin, for size consistency across a sheet |
| auto-frame | on | fires on **item** change only, never on pose change; twice (700/1800 ms) |

Distance solves each axis for the fit and takes the larger; `fieldOfView` is never touched. The status line
reports the result — `frame-fit: item 0.52x0.41 m at 2.34 m — 95% w, 88% h [height-bound]`. The bound-axis
tag is the axis that sets the size, so it's the edge that crops first past 100%; zoom scales both targets
equally, so it depends only on the item's aspect against the rect's and can't flip as the slider moves.

- **`CenterAndFit` was deliberately not reused**, even though it's right there and does the marketplace
  card. It scales and re-centres the *subject*: `root.localScale *= scaleFactor` compounds across calls,
  and re-centring moves the rig origin off the item's centre, which turns the turntable into an orbit
  instead of a spin. Leaving the rig untouched is what keeps drag/snap/turntable identical to avatar
  mode — and it works because wearables sit on the avatar's vertical axis, so a Y-spin about the rig
  root is already the right motion.
- **Bounds are whitelisted, and this is the one real trap.** The first version encapsulated every
  active renderer under the rig, and every item came out ~3x too small and sitting high in the card
  (Mauricio's screenshot). Cause: **`Avatar_Model_Idle.glb` ships a skinned mesh, `M_uBody_BaseMesh`,
  with vertex extents X -0.916..0.916, Y 0..1.911** — a T-posed reference body. It is active, its
  renderer is enabled, and nothing in the codebase disables it; it goes unnoticed only because its
  material is `baseColorFactor [0,0,0,1]`, pure black. So the fitted size became 1.91 instead of the
  shirt's 0.63, and `bounds.center` sat at mid-torso instead of chest height — which is why the item was
  both small *and* high. Fixed by **whitelisting**: only renderers with a category-named ancestor count,
  since `GLTFLoader.LoadModel` names every root after `entityDefinition.Category`. That also excludes
  emote props (root `"emote"`), and it holds in edit mode where roots sit inside
  `__OutfitStudio_EditPreview`. Blacklisting the reference mesh by name would have been the fragile
  version of this.
- Hidden geometry needs no special case: `GetComponentsInChildren<Renderer>()` without `includeInactive`
  skips inactive GameObjects, and body parts are suppressed with `SetActive(false)`. glTFast defaults to
  `skinUpdateWhenOffscreen = true`, so `renderer.bounds` is real posed-vertex bounds, not the bind-pose
  box — which is why framing is deferred ~600 ms after an apply (same reason `PreviewController` awaits
  a frame before `CenterAndFit`).
- **Fit to the card, not the frame** *(SUPERSEDED — frame-fit is the default now, see below; the
  per-axis and cube-ify reasoning still stands)*, and per-axis rather than cube-ified. The card is what reads as the
  picture, so `cardH = 1 - MarginTop - MarginBottom` and `cardW = CardWidth` (a fraction of frame
  **height** — that asymmetry is `Layout`'s, restated here) each give a candidate distance and the larger
  wins; on a portrait card, width binds. Cube-ifying to `max(x,y,z)` threw away most of the card, and
  `max(x,z)` horizontally is still stable under the only rotation this tool applies (drag, snap and
  turntable all spin about Y), so re-framing after a drag doesn't breathe. The camera is then shifted by
  the card's own vertical offset, since top and bottom margins differ and centring on the frame would sit
  the item high in the card. Verified numerically against both screenshots: recovering the shirt's true
  size from the broken shot (0.453 x 0.632 m) and re-running the new math gives 95% of the card's width
  and 88% of its height at the default padding, against ~93%/~90% measured off the target.
  `soloPadding` may go **negative** (slider -0.3..1) so an item can deliberately overspill the card.
- **Bare skin is excluded from the bounds, and pose changes never re-frame.** *(The skin exclusion is now
  the opt-in `soloFitGarmentOnly`, default OFF — see the frame-fit entry below for why. The no-reframe-on-pose
  half is still unconditional.)* Mauricio: *"why changing
  poses change the camera so much?"* Two effects, one cause — fitting the whole posed AABB. The arms and
  hands belong to the `upper_body` mesh and swing far more than the garment, so measuring across his three
  screenshots the shirt torso ran ~200 px (arm out left), ~250 px (arm up), ~315 px (arms down): a 1.6x
  swing on an identical garment. And `bounds.center` follows the extended limb, so the shirt drifted
  opposite it (arm out left -> shirt right of centre; arm up-right -> shirt down-left). Fixes:
  `ApplyPoseOnly` no longer calls `ScheduleFrameItem` (framing belongs to the item, not the pose — the
  camera must not lurch while poses are being auditioned), and the bounds now skip skin-named materials,
  matching the test `AvatarUtils.SetupWearable` already uses to decide what to tint with the skin colour.
  Accepted trade-off, chosen explicitly: an extended limb can overspill the card and crop under the masks,
  because a consistent garment size across a card sheet matters more.
- **Why skin exclusion needed per-submesh work.** Excluding whole *renderers* would have been a no-op on
  the meshes this is meant to fix: glTFast turns a glTF mesh's primitives into submeshes of ONE
  `SkinnedMeshRenderer`, so fabric and skin normally share a renderer. `TryGetGarmentBounds` therefore has
  three cases — no skin material (use `renderer.bounds`, the cheap common path), all skin (drop the
  renderer), and mixed (`BakeMesh` the posed snapshot and encapsulate only the non-skin submeshes'
  vertices, transformed by the renderer's `localToWorldMatrix`). The mixed path is **guarded**: if the
  result doesn't sit inside the renderer's own box, or exceeds its size, the assumption about BakeMesh's
  output space is wrong for this Unity version, so it keeps the whole box rather than framing on garbage.
  And the whole skin-excluding pass falls back to measuring everything if it finds *nothing* — otherwise a
  `skin`-category full-body costume whose materials are all skin-named would report "nothing to frame" at
  an item plainly on screen.
- **Fit to the frame by default, with the margin in capture pixels.** Mauricio, on an item filling ~51%
  of an 800x800 render: *"theres a lot of unused space... item should be centered and almost filling the
  frame (maybe 20 px of margin is enough)"*. The framing was doing exactly what it was built to do —
  fitting the **card**, which is `CardWidth 0.55` of frame height wide, so `0.55 / 1.05 = 52%` of frame
  width, against 51% measured; and sitting low by the card's own vertical offset, 60 px measured. Both
  numbers confirmed the card path rather than a bug. But the job this feature exists for is rendering an
  item tight and large to composite a card around it *in Photoshop*, where the frame is what bounds the
  shot, so **frame-fit is now the default** and card-fit is a toggle (`soloFitToCard`).
  `soloPadding` (a unitless fraction) is replaced by **`soloMarginPx`, default 20** — px is the unit the
  rest of the tool authors in (§20), converted against `captureHeight` so both axes lose the same physical
  amount: 20 px at 800x800 gives 95% fill and exactly 20 px on every edge, verified numerically. Existing
  presets lose their old padding value in the rename and pick up the 20 px default.
- `soloOffsetYPx` nudges vertically (positive = up) for items that don't read as centred on their
  geometric middle. And `FrameItem` now returns a **report** for the status line — `frame-fit: item
  0.52x0.41 m at 2.34 m — 95% w, 88% h` — because the two rounds of framing bugs above were both diagnosed
  by measuring pixels off a screenshot, and printing the numbers makes that immediate.
- Auto-frame runs **twice** after an item change (700 ms and 1800 ms). A slow load measured
  half-assembled would otherwise stay wrong until someone noticed, and the second pass costs nothing.
- **One "Zoom Frame (%)" slider, not two margins** *(2026-08-04, supersedes the per-axis margin bullet
  below — its measurements are what argued for this)*. Mauricio: *"those two framing sliders were basically
  zooming, and having 2 is confusing."* Exactly right, and the bullet below had already measured why: only
  the **bound** axis's margin does anything, the other one just falls out of the aspect ratio, so the pair
  presented two controls where there was one real knob plus a no-op that changes per item. `soloMarginXPx`
  and `soloMarginYPx` are replaced by **`soloZoomPct`, default 100**, which scales both axes of the target
  rect equally: 100% touches the rect on the bound axis, above that the item overspills and crops (what
  negative margins were for), below it leaves margin. The old default of 20 px at `captureHeight 2048` was
  ~98% fill, so 100 is barely a change in framing — the change is that the slider now means something
  monotonic. The bound-axis tag stays in the report: it's still the edge that crops first.
- **`soloOffsetXPx`, "Horizontal Offset (px)", default 0**, added alongside the vertical nudge for
  asymmetric items (a single earring, a staff held to one side). Positive is **right**, which image-editor
  coordinates and Unity agree on — only the vertical axis needs a sign flip. Both nudges convert px→world
  by dividing by frame **height** and scaling by `frustumHeight`, the horizontal one included: pixels are
  square, so one px is the same world distance on either axis. Default 0 rather than a tuned bias like
  `soloOffsetYPx = 70`, because an item's bounds are already centred on the avatar's vertical axis.
- **Separate X/Y margins, both allowed negative, and the readout names the bound axis** *(SUPERSEDED by the
  single zoom above; the measurements stand and are what motivated it)*. Mauricio, on a
  jacket capture: *"i need less than 0 for the Margin X, there's still a LOT of space for zooming in"*.
  Measuring that capture: 409x429 px in an 800x800 render (51% w, 54% h) and 128 px of space above against
  243 px below. Two findings. First the 51% was still the card-fit path, so it predates frame-fit. Second,
  and the useful one: **that item is height-bound**, so reducing the X margin — even below zero — changes
  nothing at all. Verified numerically: `margin X=+20,Y=+20` and `X=-40,Y=+20` both give 90.5% w / 95% h,
  identical, because the vertical axis sets the distance and the horizontal margin then just falls out of
  the aspect ratio. Reaching for Margin X to zoom in is therefore the natural move and the ineffective one,
  which is why `FrameItem`'s report now ends in **`[height-bound]`** or **`[width-bound]`** — it names the
  slider that will actually do something. Margins are split per axis (`soloMarginXPx`/`soloMarginYPx`,
  default 20 each) and both accept negatives, since an upper body with arms out legitimately wants to
  bleed sideways while still fitting top to bottom.
- **`soloOffsetYPx` follows image-editor coordinates: positive is DOWN**, not Unity's Y-up. The audience
  (§20: Figma/Photoshop artists) thinks in the former, and the sign was originally the other way — caught
  because Mauricio asked for "Y offset by default at 70" on an item measured 58 px *high*, so the value he
  wanted could only mean downward. Default **70**, correcting a systematic upward bias: an item's bounds
  centre sits below where the eye reads its centre. The exact figure is tuned against that one measurement,
  not derived, and the underlying cause is not confirmed in-editor — a candidate is geometry extending
  below the visible garment (hands, or bind-pose skinned bounds) dragging `bounds.center` down.
- **fov is never touched**, distance is. `StudioCardFrame.Layout` sizes the card from `fieldOfView`, so
  holding it constant keeps the card identical between items, and the item's perspective consistent.
- The camera is Cinemachine-driven, so the brain is disabled first — the same takeover
  `StudioFlyCamera` performs. `Release()` hands framing back, routed through
  `StudioFlyCamera.ReleaseToCinemachine()` when the fly camera owns the brain.
- Framing keeps the camera's current **rotation**, so an angle chosen by dragging or flying survives a
  re-frame.

### Per-category pose memory

EditorPrefs `OutfitStudio.ItemPose.{category}`, written from every pose-mutation site but **only while
Single-Item is the active subject** — a pose picked for a whole avatar says nothing about how a lone
jacket should hang. Picking an item applies its category's remembered pose, so every `upper_body` lands
in the chosen jacket pose instead of A-pose while a hat stays neutral. The Pose section, emote popup and
transport are reused completely unchanged; this is just a remembered default.

### Closed border for item cards

The card deliberately opens at the top so an avatar's head can overflow — `_BorderTopFade = 0.88` and
the mask's `withinX * aboveTop` keep-column. An item card wants a closed rounded rect, so
**Closed border (item card)** (`OutfitStudio.Card.ClosedBorder`, default off) pushes `_BorderTopFade = 1`
and a new `_MaskTopOpen` float that zeroes the overflow column, leaving `inside` as the card mask alone.
The top crop needs at least one mask toggle on to have a mask to crop with.

### What needed no changes at all

`StudioAvatarShaderSwitcher` (its `Apply` filters on shader name + non-persistent material, with **no
avatar check** — the item is re-shaded automatically), the card frame's geometry and shader (pure camera
math, zero avatar references), `OutfitCapture` still/video, `TurntableDriver`/`DragRotator`/`SnapRotate`,
the pose GLBs and emote transport, the px workflow and `StudioGameViewSize`, and `OutfitPreset` (which
picks up the new fields through `OutfitDefinition` — though as of 2026-08-04 it deliberately stores none of
them, via `CloneForPreset()`).

### A camera worry that turned out not to exist

The plan for this feature called for untagging `ConfiguratorCamera` first, on the theory that the studio
scene has two `MainCamera`-tagged cameras at equal depth and `OutfitCapture` resolves via `Camera.main`
(plus Recorder's `ImageSource.MainCamera`) while the card frame prefers `PreviewCamera` — so moving the
camera could make a still come from the wrong one. **Checked: not a real problem, no scene edit made.**
`ConfiguratorCamera` sits under the `Configurator` root, which is `m_IsActive: 0`, and `Bootstrap.cs:31-39`
activates exactly one branch by mode — the studio always runs builder mode, so only `Preview` is ever
live. `Camera.main` skips inactive cameras and `FindCamera()` filters on `isActiveAndEnabled`, so both
agree on `PreviewCamera`. Worth recording because §14's "the Configurator branch is the only safe
deletion" makes it look live, and reading `m_IsActive: 1` on the camera itself without walking the parent
chain is exactly how to reach the wrong conclusion.

### Status at end of session (2026-08-03)

Mauricio's words: *"its pretty good for now."* It compiles and runs — he shot several items through it
(US-flag tee, Year of the Fire Horse tee, cyberpunk jacket), so the whole path is exercised in-editor.

**Confirmed working in the editor:** body suppression (item renders alone, skeleton intact), posing via the
existing pose buttons, the shader switcher picking the item up, the card frame composing around it, stills
captured to PNG with a transparent surround, and camera framing.

**Not yet exercised, as far as I know:** video/turntable in Single-Item mode; edit-mode (non-play) preview
of a solo item; ~~preset round-trip of the solo fields~~ (inspected on 2026-08-04, found broken, and
**removed rather than fixed** — there is no round-trip any more); a `skin`-category item (would take the
all-materials-are-skin fallback path); the `BakeMesh` submesh path, which only runs with
`soloFitGarmentOnly` **on** — that default flipped off before it was ever tried, so its output-space guard
has never actually been observed to pass or fail.

**Added 2026-08-06 (the row-isolation change), all code-complete and compiled but NOT editor-verified.** The
three paths most worth an editor pass, because they're new logic rather than moved UI:

1. `ReconcileSoloSelection` — `✕` the isolated row, and a hand-edited dangling `soloUrn` after a domain
   reload. Watch specifically for a garbled slot list, which is what the re-entrancy guard exists to prevent.
2. `LoadOutfit`'s camera release — save a preset while isolated, reload it, confirm the camera is **not**
   still parked at the item's framing.
3. `_syncFramingFields` — after that same reload, the five framing controls should read defaults
   (zoom 100 / offsets 70 and 0), and the header should be plain `Framing`.

Also new and unexercised: isolating a **draft** (builder-collection) row, which is now the only route to a
solo draft since `EquipDraft`'s branch is gone; and a browser pick landing while a row is isolated, both
same-slot (displacement) and different-slot.

**Open threads for next session:**

1. **The `soloOffsetYPx = 70` default is tuned, not derived.** It corrects a real, systematic upward bias
   (measured 128 px above vs 243 px below on the jacket) but the root cause is unconfirmed. Prime suspect:
   geometry extending below the visible garment — hands, or bind-pose skinned bounds — dragging
   `bounds.center` down. Worth confirming, because if it's bind-pose bounds then `skinUpdateWhenOffscreen`
   isn't doing what §22 assumes and the fix belongs there rather than in a magic 70.
2. **Renames dropped values.** `soloPadding` → `soloMarginXPx`/`soloMarginYPx` happened within the same day,
   and on 2026-08-04 those two became `soloZoomPct`. No `FormerlySerializedAs` on any of it, so a preset
   saved before a rename loads with the new default (zoom 100 / offsets 70 and 0) rather than its authored
   framing. Harmless so far — the `Assets/*OutfitPreset*.asset` files never had solo fields written into
   them — but the next rename of a field an artist has actually tuned should carry the attribute.
   *(2026-08-06 variant of the same hazard: `soloUrn` kept its name but changed **meaning** — it must now be
   an entry of `urns`. Serialization can't express that, so `ReconcileSoloSelection` handles it at runtime
   instead. Worth remembering as the pattern for a semantic change that no attribute can migrate.)*
3. **Batch export** is the deliberate v2: queue N items, loop `set item -> await load -> frame -> capture
   still` for a whole card sheet in one click. All the pieces exist; it reuses `ScheduleFrameItem`'s
   wait-then-measure shape.
4. **Uncommitted.** Everything in §22 is working-tree only, including the new untracked
   `Editor/StudioItemCamera.cs`. The `Assets/*OutfitPreset*.asset` files are also still untracked and so
   are one `git clean` away from being lost. Usual rule: never stage the churn files
   (`URP_Asset`/`URP_GlobalSettings`/`PanelSettings`/`QualitySettings`/`EditorSettings`).
5. **Nothing was compiled by me** — the sessions that wrote this had no C# toolchain, so every claim about
   the code is inspection plus arithmetic. The arithmetic was checked against measured screenshot pixels at
   each step (that's how three separate framing bugs were found), but treat unexercised paths with suspicion.
   *(2026-08-06: no longer true, and this caveat should stop being copied forward. Unity's own Roslyn is
   runnable from WSL — recipe and its two gotchas are in "Isolate from the outfit row" above. Compile before
   claiming a change is code-complete; it costs one command and it catches the class of error that
   inspection reliably misses.)*

## 23. Stress Mode — MANA/USD in the toolbar (2026-08-04)

A **Stress Mode** toggle beside Zoom Out in the Debug tab shows a live MANA/USD rate in the toolbar,
immediately left of Clean View, refreshed every minute. An in-joke, built as asked; the notes below are only
about the two bits that aren't obvious.

**The rate source is a stand-in, and this is the open thread.** The rate was specified as *"the mana>usd
oracle (`readManaUsdRate` in `mana-rate.ts`)"* — but that file is not in this repo, which contains **no
TypeScript at all** (`find . -name "*.ts"` is empty; aang ships vendored inside `@dcl/wearable-preview`, so
it's presumably there or in the marketplace repo). Without it, the oracle's chain, contract address and
decoding were unavailable, and **guessing a contract address is the one thing not worth doing** — a wrong
feed either fails silently or, worse, returns a plausible number that nobody questions. So
`Editor/ManaRateService.cs` reads CoinGecko's keyless public price endpoint (`ids=decentraland`, which is
MANA's id there) instead. `Fetch` is the whole seam: swap its request and parse for an `eth_call` against the
aggregator and no other file changes.

- **Off by default and serialized** like the window's other toggles, because it's the only outbound request
  this tool makes *on a timer* — that shouldn't be the residue of having opened the window once.
- **One permanent 60 s scheduled item that no-ops while the toggle is off**, rather than starting and
  stopping a schedule. Same shape as `EnforceCleanGameView`'s 500 ms tick: the tick is cheap, a
  paused-item lifetime to get wrong is not. Toggling on fetches immediately so the label isn't blank for up
  to a minute.
- **Errors go in the label, never the console** (`MANA/USD —`, reason in the tooltip). A console warning
  every minute because a public endpoint rate-limited a joke feature would cost more than the joke earns.
  A zero or unparseable rate is treated as an error, since `$0.0000` in the toolbar reads as a crash.
- The completion callback checks `_manaRateLabel?.panel` — the window can be closed between request and
  response, and a write to a detached element would throw inside a callback with nothing above it to catch.

## 24. Scene and Camera settings — live light tuning (2026-08-04)

A **"Scene and Camera settings"** foldout in the outfit pane, next to Card frame, tuning the studio scene's
three lights: the directional key light (colour, intensity, **Y rotation**) and the two spotlights (colour
and intensity only). Plus **Reset lights to scene defaults**. New `Editor/StudioSceneLights.cs`; the section
is `OutfitStudioWindow.BuildSceneAndCamera`.

Named for more than the lights because the **Fly Camera** block moved in here too, from the Debug tab
(§12a) — it's a camera control, and it's tuned against the lights above it rather than against anything else
in Debug. Its settings already lived in `StudioFlyCameraController`'s EditorPrefs, so the move is pure
re-parenting: no state, no behaviour, and `CardSlider` already took its parent as an argument. The
`_configField` text box stayed behind in Debug — it's the Print Config button's output, which reads as part
of the same block only because it sat directly under it.

Section order inside the foldout: the three lights, then **Reset lights to scene defaults**, then Fly Camera.
Reset stays scoped to the lights and is worded that way; it does not touch the fly camera's speeds.

### The lights, and their authored values

Read out of `OutfitStudio.unity` on 2026-08-04. **These constants are what "Reset" means**, so if the
scene's lighting is ever re-authored they have to be re-read — once an override is applied there is nothing
to recover them from, because the override is what the light now holds.

| GameObject | colour | intensity | rotation |
|---|---|---|---|
| `Directional Light` | `(1, 0.8588, 0.4039)` warm gold | 2 | euler `(-205, 95, -37)` → **yaw 95** |
| `Spot Light Front` | `(1, 0.8078, 0.5804)` warm fill | 6 | scene's |
| `Spot Light Back` | `(0, 0.7306, 1)` cyan rim | 31.7 | scene's |

Only **Y** is exposed on the directional light, so `DIR_EULER_X = -205` / `DIR_EULER_Z = -37` are held
constant and the slider stays a pure orbit. Those come from the scene's `m_LocalEulerAnglesHint`, not from
`eulerAngles` — the latter reports the normalised `(155, 95, 323)`, which is the *same rotation* (adding 360°
to a euler component is the identity) but not the numbers in the inspector, and matching the inspector is
what makes the value checkable by eye.

### EditorPrefs, not the scene — and why nothing is written until you tune

Values live in EditorPrefs with live push from `EditorApplication.update`, the same shape
`StudioCardFrame` uses. Two consequences worth stating, because they're the whole design:

- **In edit mode, any write to a light dirties the scene.** So `Apply` returns immediately while no
  override key exists, and every write is guarded by a compare. Both steady states — untouched and tuned —
  therefore perform *zero* writes. Without that, having the window open would dirty `OutfitStudio.unity`
  every editor frame, and the scene would join the churn-file list.
- **The compare has to be tolerance-based, not equality.** Colours round-trip through an 8-bit hex string
  (`ColorUtility`, matching the card frame's storage), so `0.7306` comes back as `186/255 = 0.7294`; and for
  rotation, two different eulers can be one rotation and quaternion sign isn't unique either. Comparing raw
  numbers would write every frame — `Quaternion.Angle > 0.01°` and `Mathf.Approximately` per channel
  instead.

`HasOverrides` is cached in a `bool?` invalidated by the setters, so the per-frame path costs one bool test
rather than seven `EditorPrefs.HasKey` calls.

### Reset, and the studio-scene gate

`ResetToSceneDefaults()` deletes the keys **and then explicitly pushes the authored constants**. The second
half is the part that's easy to miss: with the keys gone `Apply` is a no-op, so without an explicit push the
lights would just keep whatever the last tuning left them at and Reset would appear to do nothing.

Everything is gated on the active scene being `STUDIO_SCENE_PATH`, the same gate
`StudioCardFrame`/`StudioAvatarShaderSwitcher`/`StudioFlyCameraController` use. Here it's load-bearing
rather than tidy: **`Main.unity` has a `Directional Light` too** (and no spotlights), so an ungated Reset
would push studio values onto the production scene's lighting, in the one place that would actually ship.

Lights are matched **by name** because they're plain scene objects with no marker component, and adding one
would be a scene edit made in order to avoid scene edits.
