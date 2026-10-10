# Disk Cache

The disk cache persists downloaded assets across sessions so that revisiting content does not re-download it. It lives at `{persistentDataPath}/DiskCacheV3` (see `CacheDirectory`); the `V3` suffix is a version stamp — bumping `CacheDirectory.CACHE_VERSION` wipes older cache directories on startup when a breaking format change is introduced.

Entry points live under `Explorer/Assets/DCL/Infrastructure/ECS/StreamableLoading/Cache/Disk/`.

## What is cached

| Content | Extension | Wired in |
|---------|-----------|----------|
| Textures (`TextureData`) | `.tex` | `LoadTextureSystem` via `DiskCacheOptions` |
| Scene JS sources | `.js` | `CachedWebJsSources` |
| ISS descriptors | `.iss.json` | `LoadISSDescriptorSystem` (`GlobalWorldFactory`) |
| Partial asset-bundle downloads | `.partial` (in `partials/` subdirectory, separate `DiskCache` instance) | `PartialDownloadSystemBase` |

Only assets fetched from the **web** qualify (`ILoadingIntention.IsQualifiedForDiskCache()` — `CurrentSource == AssetSource.WEB`); embedded/local content is already on disk.

## Architecture

- **`DiskCache`** (raw layer) — reads/writes byte streams to files. Concurrency between readers/writers of the same entry is guarded by `FilesLock` (in-process only). After each successful write it gives `IDiskCleanUp` a chance to run.
- **`DiskCache<T, Ts>`** (typed layer) — wraps the raw layer with an `IDiskSerializer<T, Ts>` that converts the asset to/from bytes (e.g. `TextureDiskSerializer`, `StringDiskSerializer`). Serialization streams through pooled 128 KB chunks (`SerializeMemoryIterator`).
- **`GenericCache<T, TKey>`** — composes the memory cache (the `IStreamableCache` of the load system) with the typed disk cache. This is what `LoadSystemBase` talks to: reads check memory first, then disk (a disk hit backfills memory); writes go to both.

### Cache keys

Each intention type provides an `IDiskHashCompute` (e.g. `GetTextureIntention.DiskHashCompute`) that feeds the identity of the request — content file hash (or URL when no hash exists), plus parameters that change the stored bytes, such as wrap/filter modes for textures — into a SHA256. The hex digest plus the extension is the file name. Include an `ITERATION_NUMBER` bump in the payload when the serialized format changes, so stale entries of the old format are simply never found.

## How it interacts with asset loading

Within `LoadSystemBase.CacheableFlowAsync` (see [Asset Promises](asset-promises.md)):

1. **Read** — `TryLoadFromCacheAsync` asks `GenericCache`: memory hit → done; disk hit → deserialize, backfill memory, done; miss → download.
2. **Write** — after a successful download, the result is written to memory + disk as **fire-and-forget**, using the *system's* lifetime token rather than the intention's. This is deliberate: once the asset is in memory, the disk write must run to completion. If it were tied to the intention, a short-lived consumer (e.g. an entity destroyed a second after spawning) would cancel the write mid-stream.

## Integrity guarantees

A truncated or corrupt entry served as valid cache content is far worse than a miss, so the cache maintains the invariant that **a file at its final path is always a complete entry**:

- **Atomic writes** — `DiskCache.PutAsync` streams into a `<name>.<ext>.tmp` file and swaps it into the final path only after the last chunk is written. Cancellation, exceptions, or a crash mid-write can never leave partial data at the final path; the temp file is deleted on any failure (best-effort) and ignored by clean-up.
- **Interrupted overwrites keep the previous entry** — the existing complete file is only replaced at swap time.
- **Corrupt entries self-heal** — if deserialization of a stored entry throws (e.g. an entry written by an older client before atomic writes), `DiskCache<T, Ts>.ContentAsync` deletes the file and reports an error, which `GenericCache` treats as a miss: the asset is re-downloaded and re-cached instead of failing on the same entry on every session.

The motivating incident: a texture write cancelled mid-stream (entity despawned) left a truncated `.tex` entry; on every later session the deserialization exception escaped the load flow and left a dangling entry in the cache's `OngoingRequests`, so every subsequent request for that texture silently awaited forever — no web request, no error, no texture. `LoadSystemBase.CacheableFlowAsync` now cleans up its ongoing-request registration on *any* exception, not only cancellations. Tests covering these behaviors: `DiskCacheShould`, `LoadSystemBaseUnexpectedExceptionShould`.

## Eviction

`LRUDiskCleanUp` keeps the directory under a size budget (1 GB by default). When a write pushes the total over budget, files are deleted in least-recently-used order (by last access time; reads touch entries via `NotifyUsed`) until the size is back under budget. `.tmp` files are excluded from clean-up bookkeeping — they are in-flight writes, not entries.

## Disabling and troubleshooting

- **Local Scene Development** mode disables the disk cache entirely (`MainSceneLoader`), so local iteration always fetches fresh content. Keep this in mind when a bug reproduces only against a deployed realm: LSD and production runs have different caching behavior.
- `--disable-disk-cache` / `--disable-disk-cache-cleanup` — see [App Arguments](app-arguments.md).
- **Decentraland → Cache → Clear Disk Cache** menu item deletes the cache directory from the Editor.
- The `/cache` chat command opens the cache folder at runtime (like `/logs` for the logs folder).
- Cache locations: Editor (macOS) `~/Library/Application Support/Decentraland/Explorer/DiskCacheV3`; built player (macOS) `~/Library/Application Support/com.Decentraland.Explorer/DiskCacheV3`; on Windows under `%userprofile%/AppData/LocalLow/<company>/<product>/DiskCacheV3`.
