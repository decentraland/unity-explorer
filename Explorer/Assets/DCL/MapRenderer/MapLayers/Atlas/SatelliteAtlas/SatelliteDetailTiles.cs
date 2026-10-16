using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MapRenderer.Culling;
using DCL.Optimization.Hashing;
using DCL.Utility.Types;
using DCL.WebRequests;
using DG.Tweening;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    /// <summary>
    ///     Streams the satellite map's <see cref="TILE_PIXELS" /> px KTX2 tiles, levels 3 to 8, over the bundled level-3 chunks, which
    ///     stay underneath while a tile loads or when it fails. Paths come from the <see cref="SatelliteManifest" />, fetched before any tile.
    ///     Level L splits the bundled grid into 2^L x 2^L tiles (i eastward, j southward), so a level-3 tile covers one bundled chunk.
    ///     Every map camera gets the level-3 tiles in view and, on top of them, the level whose sharpness is nearest to its render
    ///     texture's when that level is finer. Tiles are requested once the cameras have been still for <see cref="SETTLE_SECONDS" />,
    ///     at most <see cref="MAX_CONCURRENT_LOADS" /> at a time, nearest to their camera's centre first; a load whose tile leaves the view
    ///     is cancelled. A tile that fails is requested again after <see cref="RETRY_FAILED_TILE_AFTER_SECONDS" />, even if no camera moves,
    ///     and keeps that wait if it leaves the view and comes back in between; a tile the server answers 404 for is never requested again
    ///     in the session. Each tile's KTX2 file is kept in the disk cache, so a later load reads it from disk, and a file that fails to decode is removed
    ///     from it. The decoded texture keeps no CPU copy and belongs to the tile alone. Level-3 tiles are the baseline: once the manifest
    ///     loads, the ones already in the disk cache are loaded without any camera showing them, and no level-3 tile is ever dropped or
    ///     evicted, so each one covers its bundled chunk for the rest of the session. Up to <see cref="MAX_CACHED_TILES" /> finer tiles out
    ///     of view stay loaded while the same cameras are rented; once a camera is released, every finer tile out of view is destroyed and
    ///     its memory freed.
    /// </summary>
    internal class SatelliteDetailTiles : IDisposable
    {
        internal const int BASE_LEVEL = 3;
        internal const int MAX_LEVEL = 8;
        internal const int MAX_TILES_PER_CAMERA = 64;
        internal const int MAX_CONCURRENT_LOADS = 8;
        private const int TILE_PIXELS = 1024;
        private const float SHARPNESS_BIAS = 0.4f;
        private const int MAX_CACHED_TILES = 16;
        private const string MANIFEST_FILE = "manifest.json";
        private const string CACHE_EXTENSION = "ktx2";
        private const int CACHE_ITERATION = 1;
        private const float SETTLE_SECONDS = 0.1f;
        private const float FADE_IN_SECONDS = 0.25f;
        internal const float RETRY_FAILED_TILE_AFTER_SECONDS = 30f;
        private const float SATURATION_VALUE = 1f;
        private static readonly int SATURATION = Shader.PropertyToID("_Saturation");

        private static readonly IComparer<KeyValuePair<int, Vector3Int>> OLDEST_FIRST =
            Comparer<KeyValuePair<int, Vector3Int>>.Create(static (a, b) => a.Key.CompareTo(b.Key));

        /// <summary>Tests replace the clock to pass the retry wait without waiting it out.</summary>
        internal Func<float>? clockOverride;

        /// <summary>Tests replace the KTX2 decoder to control when a tile's texture is produced, and which one.</summary>
        internal Func<byte[], string, UniTask<Texture2D>>? decodeOverride;

        private readonly string baseUrl;
        private readonly IWebRequestController webRequestController;
        private readonly IDiskCache<byte[]> diskCache;
        private readonly IMapCullingController cullingController;
        private readonly AtlasChunk template;
        private readonly int drawOrderOfBaseLevel;
        private readonly CancellationTokenSource lifetimeCts = new ();

        private readonly Dictionary<Vector3Int, Tile> tiles = new ();
        private readonly Stack<AtlasChunk> pooledViews = new ();
        private readonly List<Vector3Int> pending = new ();
        private readonly NearestFirstComparer nearestFirst;
        private readonly List<Vector3Int> unusedLoads = new ();
        private readonly List<KeyValuePair<int, Vector3Int>> evictions = new ();

        // Kept for the session, by URL, past the tiles' own lifetime: a tile out of view is dropped from <see cref="tiles" />.
        private readonly HashSet<string> emptyUrls = new ();
        private readonly Dictionary<string, float> failedUrls = new ();

        // A completion source takes every awaiter while the load is pending; a preserved task takes only one until it completes.
        private readonly UniTaskCompletionSource<SatelliteManifest> manifestLoad = new ();

        private Vector2 gridTopLeft;
        private float baseChunkSize;
        private int refreshStamp;
        private int cameraCount;
        private float lastCameraChangeTime;
        private float lastFailureTime;
        private int activeLoads;
        private bool settlePending;
        private bool pumping;
        private bool retryPending;
        private bool tileFailureReported;
        private bool manifestFailureReported;
        private bool diskCacheFailureReported;

        internal int ActiveLoads => activeLoads;

        private float Now => clockOverride?.Invoke() ?? UnityEngine.Time.realtimeSinceStartup;

        public SatelliteDetailTiles(string baseUrl, IWebRequestController webRequestController, IDiskCache<byte[]> diskCache, IMapCullingController cullingController,
            SpriteRenderer template, int drawOrderOfBaseLevel)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            this.webRequestController = webRequestController;
            this.diskCache = diskCache;
            this.cullingController = cullingController;
            this.drawOrderOfBaseLevel = drawOrderOfBaseLevel;
            nearestFirst = new NearestFirstComparer(tiles);

            template.material.SetFloat(SATURATION, SATURATION_VALUE);
            this.template = template.GetComponent<AtlasChunk>();
            template.gameObject.SetActive(false);
        }

        /// <summary>Aligns the tiles with the bundled grid: its top-left corner and one bundled chunk's side, in local units.</summary>
        public void Initialize(Vector2 bundledGridTopLeft, float bundledChunkSize)
        {
            gridTopLeft = bundledGridTopLeft;
            baseChunkSize = bundledChunkSize;

            PublishManifestAsync(lifetimeCts.Token).Forget();
            cullingController.CamerasChanged += Refresh;
            Refresh();
            LoadCachedBaseLevelAsync(lifetimeCts.Token).Forget();
        }

        public void Dispose()
        {
            cullingController.CamerasChanged -= Refresh;
            lifetimeCts.SafeCancelAndDispose();
            pending.Clear();

            foreach (Tile tile in tiles.Values)
                ReleaseTile(tile);

            tiles.Clear();

            while (pooledViews.Count > 0)
                UnityObjectUtils.SafeDestroy(pooledViews.Pop().gameObject);

            // The instance created by template.material, shared by the template and every pooled view.
            UnityObjectUtils.SafeDestroy(template.MainSpriteRenderer.sharedMaterial);
            UnityObjectUtils.SafeDestroy(template.gameObject);
        }

        /// <summary>
        ///     Marks the tiles every active camera shows, drops the loads of finer tiles that left the view, releases the oldest loaded
        ///     finer tiles over the cap (all of them out of view once a camera was released), and schedules the missing tiles' requests for when
        ///     the cameras settle. Does nothing while the satellite layer is hidden.
        /// </summary>
        internal void Refresh()
        {
            if (!template.transform.parent.gameObject.activeInHierarchy)
                return;

            refreshStamp++;
            lastCameraChangeTime = Now;

            int previousCameraCount = cameraCount;
            cameraCount = cullingController.CameraStates.Count;

            VisitVisibleTiles(requestWith: null);

            CollectUnusedLoads(tiles, refreshStamp, unusedLoads);

            for (var k = 0; k < unusedLoads.Count; k++)
                if (tiles.Remove(unusedLoads[k], out Tile? dropped))
                    ReleaseTile(dropped);

            CollectEvictions(tiles, refreshStamp, cameraCount < previousCameraCount ? 0 : MAX_CACHED_TILES, evictions);

            for (var k = 0; k < evictions.Count; k++)
                if (tiles.Remove(evictions[k].Value, out Tile? evicted))
                    ReleaseTile(evicted);

            if (!settlePending)
                RequestWhenSettledAsync(lifetimeCts.Token).Forget();
        }

        /// <summary>
        ///     Releases every finer-than-<see cref="BASE_LEVEL" /> tile, loaded or loading, for when the satellite layer is hidden:
        ///     <see cref="Refresh" /> does nothing until it shows again.
        /// </summary>
        internal void ReleaseFinerTiles()
        {
            // No tile was used in a new refresh, so the eviction takes them all.
            refreshStamp++;
            CollectEvictions(tiles, refreshStamp, 0, evictions);

            for (var k = 0; k < evictions.Count; k++)
                if (tiles.Remove(evictions[k].Value, out Tile? evicted))
                    ReleaseTile(evicted);
        }

        /// <summary>
        ///     The level whose pixel density is nearest to <paramref name="screenPixelsPerUnit" /> on a log scale after a
        ///     <see cref="SHARPNESS_BIAS" /> toward the sharper level, so a tile is shown between 0.54x and 1.07x its size: magnified
        ///     tiles look soft, while the next sharper level would cost four times the bytes for the same view.
        ///     <paramref name="baseLevelPixelsPerUnit" /> is the density of a <see cref="BASE_LEVEL" /> tile.
        /// </summary>
        internal static int LevelFor(float screenPixelsPerUnit, float baseLevelPixelsPerUnit)
        {
            int level = BASE_LEVEL + Mathf.RoundToInt(Mathf.Log(screenPixelsPerUnit / baseLevelPixelsPerUnit, 2f) + SHARPNESS_BIAS);
            return Mathf.Clamp(level, BASE_LEVEL, MAX_LEVEL);
        }

        /// <summary>Half-open tile index range of <paramref name="level" /> that <paramref name="rect" /> overlaps, clamped to the grid.</summary>
        internal static RectInt TileRange(Rect rect, int level, Vector2 gridTopLeft, float bundledChunkSize)
        {
            int side = 1 << level;
            float tileSize = TileSize(level, bundledChunkSize);

            int minI = Mathf.Clamp(Mathf.FloorToInt((rect.xMin - gridTopLeft.x) / tileSize), 0, side - 1);
            int maxI = Mathf.Clamp(Mathf.FloorToInt((rect.xMax - gridTopLeft.x) / tileSize), 0, side - 1);
            int minJ = Mathf.Clamp(Mathf.FloorToInt((gridTopLeft.y - rect.yMax) / tileSize), 0, side - 1);
            int maxJ = Mathf.Clamp(Mathf.FloorToInt((gridTopLeft.y - rect.yMin) / tileSize), 0, side - 1);

            return new RectInt(minI, minJ, maxI - minI + 1, maxJ - minJ + 1);
        }

        /// <summary>
        ///     Steps <paramref name="level" /> down until <paramref name="rect" /> needs at most <see cref="MAX_TILES_PER_CAMERA" /> tiles,
        ///     stopping at <see cref="BASE_LEVEL" />, whose 8x8 tiles always fit. Outputs the tile range of the returned level.
        /// </summary>
        internal static int LevelWithinTileBudget(Rect rect, int level, Vector2 gridTopLeft, float bundledChunkSize, out RectInt range)
        {
            range = TileRange(rect, level, gridTopLeft, bundledChunkSize);

            while (level > BASE_LEVEL && range.width * range.height > MAX_TILES_PER_CAMERA)
            {
                level--;
                range = TileRange(rect, level, gridTopLeft, bundledChunkSize);
            }

            return level;
        }

        /// <summary>Centre of the tile <paramref name="id" /> (x, y, level) in local units.</summary>
        internal static Vector2 TileCenter(Vector3Int id, Vector2 gridTopLeft, float bundledChunkSize)
        {
            float tileSize = TileSize(id.z, bundledChunkSize);
            return new Vector2(gridTopLeft.x + ((id.x + 0.5f) * tileSize), gridTopLeft.y - ((id.y + 0.5f) * tileSize));
        }

        /// <summary>
        ///     Fills <paramref name="result" /> with the finer-than-<see cref="BASE_LEVEL" /> tiles that have no texture and were not used
        ///     in the refresh <paramref name="refreshStamp" />: pending or loading tiles that left the view, and failed tiles out of view.
        /// </summary>
        internal static void CollectUnusedLoads(Dictionary<Vector3Int, Tile> tiles, int refreshStamp, List<Vector3Int> result)
        {
            result.Clear();

            foreach ((Vector3Int id, Tile tile) in tiles)
                if (id.z != BASE_LEVEL && tile.Texture == null && tile.LastUsed != refreshStamp)
                    result.Add(id);
        }

        /// <summary>
        ///     Fills <paramref name="result" /> with the finer-than-<see cref="BASE_LEVEL" /> tiles to release to get back to
        ///     <paramref name="maxCachedTiles" /> of them, oldest first. Tiles used in the refresh <paramref name="refreshStamp" /> and
        ///     <see cref="BASE_LEVEL" /> tiles are never released.
        /// </summary>
        internal static void CollectEvictions(Dictionary<Vector3Int, Tile> tiles, int refreshStamp, int maxCachedTiles, List<KeyValuePair<int, Vector3Int>> result)
        {
            result.Clear();

            int excess = -maxCachedTiles;

            foreach (Vector3Int id in tiles.Keys)
                if (id.z != BASE_LEVEL)
                    excess++;

            if (excess <= 0)
                return;

            foreach ((Vector3Int id, Tile tile) in tiles)
                if (id.z != BASE_LEVEL && tile.LastUsed != refreshStamp)
                    result.Add(new KeyValuePair<int, Vector3Int>(tile.LastUsed, id));

            result.Sort(OLDEST_FIRST);

            if (result.Count > excess)
                result.RemoveRange(excess, result.Count - excess);
        }

        private static float TileSize(int level, float bundledChunkSize) =>
            bundledChunkSize * (1 << BASE_LEVEL) / (1 << level);

        /// <summary>
        ///     Waits for the manifest and until no camera has changed for <see cref="SETTLE_SECONDS" />, then queues the visible tiles
        ///     that are missing. The manifest decides which tiles exist and at which paths, so none is requested before it.
        /// </summary>
        private async UniTaskVoid RequestWhenSettledAsync(CancellationToken ct)
        {
            settlePending = true;

            try
            {
                SatelliteManifest manifest = await manifestLoad.Task;

                while (true)
                {
                    float remaining = SETTLE_SECONDS - (Now - lastCameraChangeTime);

                    if (remaining <= 0f)
                        break;

                    await UniTask.Delay(TimeSpan.FromSeconds(remaining), ignoreTimeScale: true, cancellationToken: ct);
                }

                if (template.transform.parent.gameObject.activeInHierarchy)
                {
                    VisitVisibleTiles(manifest);
                    PumpLoads();
                }
            }
            catch (OperationCanceledException) { }
            finally { settlePending = false; }
        }

        /// <summary>
        ///     Once the manifest loads, queues every <see cref="BASE_LEVEL" /> tile it lists that no camera has requested yet, to be read
        ///     from the disk cache only: a tile that isn't stored there is dropped and left to be downloaded when a camera shows it.
        /// </summary>
        private async UniTaskVoid LoadCachedBaseLevelAsync(CancellationToken ct)
        {
            SatelliteManifest manifest;

            try { manifest = await manifestLoad.Task; }
            catch (OperationCanceledException) { return; }

            if (ct.IsCancellationRequested)
                return;

            int side = 1 << BASE_LEVEL;

            for (var j = 0; j < side; j++)
            for (var i = 0; i < side; i++)
            {
                var id = new Vector3Int(i, j, BASE_LEVEL);

                if (tiles.ContainsKey(id) || !manifest.TryGetTilePath(id, out string path))
                    continue;

                var tile = new Tile(RentView()) { Url = $"{baseUrl}/{path}", CacheOnly = true, Pending = true };

                // Behind every request for a tile a camera shows.
                tile.DistanceSqToCamera = float.MaxValue;
                tiles.Add(id, tile);
                pending.Add(id);
            }

            PumpLoads();
        }

        /// <summary>
        ///     Marks every tile inside an active camera as used in the current refresh and, given <paramref name="requestWith" />,
        ///     queues the missing ones at the paths it lists.
        /// </summary>
        private void VisitVisibleTiles(SatelliteManifest? requestWith)
        {
            float baseLevelPixelsPerUnit = TILE_PIXELS / baseChunkSize;
            IReadOnlyList<CameraState> cameras = cullingController.CameraStates;

            for (var c = 0; c < cameras.Count; c++)
            {
                CameraState camera = cameras[c];

                if (!camera.CameraController.Camera.isActiveAndEnabled)
                    continue;

                Rect rect = camera.Rect;
                Vector2 center = rect.center;

                UseRange(TileRange(rect, BASE_LEVEL, gridTopLeft, baseChunkSize), BASE_LEVEL, requestWith, center);

                float screenPixelsPerUnit = camera.CameraController.GetRenderTexture().height / rect.height;
                int level = LevelFor(screenPixelsPerUnit, baseLevelPixelsPerUnit);

                if (level == BASE_LEVEL)
                    continue;

                level = LevelWithinTileBudget(rect, level, gridTopLeft, baseChunkSize, out RectInt range);

                if (level > BASE_LEVEL)
                    UseRange(range, level, requestWith, center);
            }
        }

        private void UseRange(RectInt range, int level, SatelliteManifest? requestWith, Vector2 cameraCenter)
        {
            for (int j = range.yMin; j < range.yMax; j++)
            for (int i = range.xMin; i < range.xMax; i++)
                Use(new Vector3Int(i, j, level), requestWith, cameraCenter);
        }

        private void Use(Vector3Int id, SatelliteManifest? requestWith, Vector2 cameraCenter)
        {
            if (tiles.TryGetValue(id, out Tile? tile))
            {
                tile.LastUsed = refreshStamp;

                // A queued tile is ordered by its distance to the camera that shows it now; a cache-only one is downloaded if the cache misses.
                if (requestWith != null && (tile.Pending || tile.CacheOnly))
                {
                    tile.CacheOnly = false;
                    tile.DistanceSqToCamera = (TileCenter(id, gridTopLeft, baseChunkSize) - cameraCenter).sqrMagnitude;
                }

                if (requestWith != null && tile.FailedAt is { } failedAt && Now - failedAt >= RETRY_FAILED_TILE_AFTER_SECONDS)
                {
                    tile.FailedAt = null;
                    failedUrls.Remove(tile.Url);
                    Enqueue(id, tile, cameraCenter);
                }

                return;
            }

            if (requestWith == null)
                return;

            // A tile of a section the manifest doesn't list is not published: there is nothing to request.
            if (!requestWith.TryGetTilePath(id, out string path))
                return;

            var url = $"{baseUrl}/{path}";

            if (emptyUrls.Contains(url))
                return;

            tile = new Tile(RentView()) { LastUsed = refreshStamp, Url = url };
            tiles.Add(id, tile);

            // Back in view before its retry wait is over: it waits the rest of it, as if it had never left.
            if (failedUrls.TryGetValue(url, out float failedBefore) && Now - failedBefore < RETRY_FAILED_TILE_AFTER_SECONDS)
            {
                tile.FailedAt = failedBefore;
                return;
            }

            failedUrls.Remove(url);
            Enqueue(id, tile, cameraCenter);
        }

        private void Enqueue(Vector3Int id, Tile tile, Vector2 cameraCenter)
        {
            tile.Pending = true;
            tile.DistanceSqToCamera = (TileCenter(id, gridTopLeft, baseChunkSize) - cameraCenter).sqrMagnitude;
            pending.Add(id);
        }

        /// <summary>Starts the pending loads nearest to their camera's centre, keeping at most <see cref="MAX_CONCURRENT_LOADS" /> in flight.</summary>
        private void PumpLoads()
        {
            // A load that completes synchronously pumps from inside this loop; the loop already reuses the slot it frees.
            if (pumping || pending.Count == 0)
                return;

            pumping = true;

            try
            {
                pending.Sort(nearestFirst);
                var consumed = 0;

                for (; consumed < pending.Count && activeLoads < MAX_CONCURRENT_LOADS; consumed++)
                {
                    // A queued tile may have been dropped, or started through a duplicate entry, since it was queued.
                    if (tiles.TryGetValue(pending[consumed], out Tile? tile) && tile.Pending)
                    {
                        tile.Pending = false;
                        activeLoads++;
                        LoadAsync(pending[consumed], tile).Forget();
                    }
                }

                pending.RemoveRange(0, consumed);
            }
            finally { pumping = false; }
        }

        private async UniTaskVoid LoadAsync(Vector3Int id, Tile tile)
        {
            CancellationToken ct = tile.Cts.Token;
            string url = tile.Url;
            Texture2D? texture = null;
            Exception? failure = null;

            try
            {
                (byte[]? bytes, bool downloaded) = await FetchAsync(url, tile, ct);

                await UniTask.SwitchToMainThread(ct);

                if (bytes != null)
                {
                    try
                    {
                        texture = decodeOverride != null
                            ? await decodeOverride(bytes, url)
                            : await KtxTextureDecoder.DecodeAsync(bytes, linear: false, TextureWrapMode.Clamp, FilterMode.Bilinear, readable: false, url);
                    }

                    // A missing native decoder is the machine's, not the file's.
                    catch (Exception e) when (!downloaded && e is not (OperationCanceledException or DllNotFoundException or EntryPointNotFoundException))
                    {
                        // Remove the stored file so the tile's next attempt downloads it again instead of decoding the same bytes.
                        RemoveCachedFileAsync(url).Forget();
                        throw;
                    }

                    // Only bytes that decode are stored. The write runs on the streamer's lifetime: the tile can be dropped before it ends.
                    if (downloaded && !lifetimeCts.IsCancellationRequested)
                        StoreAsync(url, bytes, lifetimeCts.Token).Forget();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { failure = e; }

            // A request can fail on another thread; the tiles and their views are only touched on the main one.
            await UniTask.SwitchToMainThread();
            activeLoads--;

            // The tile was dropped while loading: it no longer owns anything, so the texture is destroyed here.
            if (ct.IsCancellationRequested)
                UnityObjectUtils.SafeDestroy(texture);
            else if (texture != null)
                Show(id, tile, texture);

            // Not in the disk cache, or its stored file failed to decode, and no camera shows it: nothing to download for it yet.
            else if (tile.CacheOnly)
            {
                tiles.Remove(id);
                ReleaseTile(tile);
            }
            else if (failure != null)
                Fail(id, tile, failure);

            // A camera showed the tile while its disk read missed: it is downloaded now.
            else
            {
                tile.Pending = true;
                pending.Add(id);
            }

            PumpLoads();
        }

        /// <summary>The tile's KTX2 file and whether it was downloaded. Null bytes on a cache miss while the tile is cache-only.</summary>
        private async UniTask<(byte[]? bytes, bool downloaded)> FetchAsync(string url, Tile tile, CancellationToken ct)
        {
            EnumResult<Option<byte[]>, TaskError> cached;

            using (HashKey key = NewCacheKey(url))
                cached = await diskCache.ContentAsync(key, CACHE_EXTENSION, ct);

            ct.ThrowIfCancellationRequested();

            if (cached.Error is { } error)
                ReportDiskCacheFailure(error.Message);
            else if (cached.Value.Has)
                return (cached.Value.Value, false);

            if (tile.CacheOnly)
                return (null, false);

            byte[] bytes = await webRequestController
                                .GetAsync(new CommonArguments(URLAddress.FromString(url), RetryPolicy.WithRetries(1)), ct, ReportCategory.UI, suppressErrors: true)
                                .GetDataCopyAsync();

            return (bytes, true);
        }

        private async UniTaskVoid StoreAsync(string url, byte[] bytes, CancellationToken ct)
        {
            try
            {
                using HashKey key = NewCacheKey(url);
                EnumResult<TaskError> result = await diskCache.PutAsync(key, CACHE_EXTENSION, bytes, ct);

                if (result.Error is { } error)
                    ReportDiskCacheFailure(error.Message);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
        }

        private async UniTaskVoid RemoveCachedFileAsync(string url)
        {
            try
            {
                // A decode can fail after Dispose, and the disposed source's token throws.
                if (lifetimeCts.IsCancellationRequested)
                    return;

                using HashKey key = NewCacheKey(url);
                EnumResult<TaskError> result = await diskCache.RemoveAsync(key, CACHE_EXTENSION, lifetimeCts.Token);

                if (result.Error is { } error)
                    ReportDiskCacheFailure(error.Message);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
        }

        private static HashKey NewCacheKey(string url) =>
            HashKey.FromString($"{CACHE_ITERATION}:{url}");

        /// <summary>Once per session: a disabled or failing disk cache affects every tile the same way.</summary>
        private void ReportDiskCacheFailure(string? message)
        {
            if (diskCacheFailureReported)
                return;

            diskCacheFailureReported = true;
            ReportHub.Log(ReportCategory.UI, $"Satellite tiles are not using the disk cache: {message}");
        }

        private async UniTaskVoid PublishManifestAsync(CancellationToken ct)
        {
            try { manifestLoad.TrySetResult(await LoadManifestAsync(ct)); }
            catch (OperationCanceledException) { manifestLoad.TrySetCanceled(ct); }
        }

        /// <summary>
        ///     The manifest. A base URL that answers 404 publishes none, so its tiles are at the unversioned paths.
        ///     Any other failure is retried every <see cref="RETRY_FAILED_TILE_AFTER_SECONDS" />: requesting tiles without the
        ///     manifest would show stale ones, or mark published ones empty.
        /// </summary>
        private async UniTask<SatelliteManifest> LoadManifestAsync(CancellationToken ct)
        {
            var url = $"{baseUrl}/{MANIFEST_FILE}";

            while (true)
            {
                try
                {
                    SatelliteManifest.Dto dto = await webRequestController
                                                     .GetAsync(new CommonArguments(URLAddress.FromString(url), RetryPolicy.NONE), ct, ReportCategory.UI, suppressErrors: true)
                                                     .CreateFromJson<SatelliteManifest.Dto>(WRJsonParser.Newtonsoft);

                    return SatelliteManifest.From(dto);
                }
                catch (OperationCanceledException) { throw; }
                catch (UnityWebRequestException e) when (e.ResponseCode == WebRequestUtils.NOT_FOUND) { return SatelliteManifest.UNVERSIONED; }
                catch (Exception e)
                {
                    // One report per session: an offline or wrong URL fails every attempt the same way.
                    if (!manifestFailureReported)
                    {
                        manifestFailureReported = true;
                        ReportHub.LogException(e, ReportCategory.UI);
                    }
                }

                await UniTask.Delay(TimeSpan.FromSeconds(RETRY_FAILED_TILE_AFTER_SECONDS), ignoreTimeScale: true, cancellationToken: ct);
            }
        }

        private void Fail(Vector3Int id, Tile tile, Exception failure)
        {
            // A tile the server doesn't have is empty for the session: there is nothing to retry or report, and no view to hold.
            if (failure is UnityWebRequestException { ResponseCode: WebRequestUtils.NOT_FOUND })
            {
                emptyUrls.Add(tile.Url);
                tiles.Remove(id);
                ReleaseTile(tile);
                return;
            }

            // One report per session: a wrong or offline URL fails every tile in view.
            if (!tileFailureReported)
            {
                tileFailureReported = true;
                ReportHub.LogException(failure, ReportCategory.UI);
            }

            tile.FailedAt = lastFailureTime = Now;
            failedUrls[tile.Url] = lastFailureTime;

            if (!retryPending)
                RefreshWhenFailedTilesCanRetryAsync(lifetimeCts.Token).Forget();
        }

        /// <summary>
        ///     Refreshes once every failed tile can be retried: refreshes otherwise only follow camera changes, so a tile under a
        ///     camera that stays still would never be requested again.
        /// </summary>
        private async UniTaskVoid RefreshWhenFailedTilesCanRetryAsync(CancellationToken ct)
        {
            retryPending = true;

            while (true)
            {
                float remaining = RETRY_FAILED_TILE_AFTER_SECONDS - (Now - lastFailureTime);

                if (remaining <= 0f)
                    break;

                if (await UniTask.Delay(TimeSpan.FromSeconds(remaining), ignoreTimeScale: true, cancellationToken: ct).SuppressCancellationThrow())
                    return;
            }

            retryPending = false;
            Refresh();
        }

        private void Show(Vector3Int id, Tile tile, Texture2D texture)
        {
            tile.Texture = texture;
            float tileSize = TileSize(id.z, baseChunkSize);

            tile.Sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, texture.width / tileSize, 0, SpriteMeshType.FullRect, Vector4.one, false);

            SpriteRenderer renderer = tile.View.MainSpriteRenderer;
            renderer.sprite = tile.Sprite;
            renderer.sortingOrder = drawOrderOfBaseLevel + id.z - BASE_LEVEL;
            renderer.color = AtlasChunkConstants.INITIAL_COLOR;
            renderer.enabled = true;

            tile.View.transform.localPosition = TileCenter(id, gridTopLeft, baseChunkSize);
            tile.View.gameObject.SetActive(true);
            renderer.DOColor(AtlasChunkConstants.FINAL_COLOR, FADE_IN_SECONDS);
        }

        private AtlasChunk RentView()
        {
            if (pooledViews.Count > 0)
                return pooledViews.Pop();

            AtlasChunk view = Object.Instantiate(template, template.transform.parent);
            view.LoadingSpriteRenderer.gameObject.SetActive(false);

#if UNITY_EDITOR
            view.gameObject.name = "Satellite detail tile";
#endif

            return view;
        }

        /// <summary>Cancels the tile's load, destroys its sprite and texture, and pools its view.</summary>
        private void ReleaseTile(Tile tile)
        {
            tile.Cts.SafeCancelAndDispose();
            UnityObjectUtils.SafeDestroy(tile.Sprite);
            UnityObjectUtils.SafeDestroy(tile.Texture);

            // On application quit the view can already be destroyed.
            if (!tile.View)
                return;

            SpriteRenderer renderer = tile.View.MainSpriteRenderer;
            renderer.DOKill();
            renderer.sprite = null;
            tile.View.gameObject.SetActive(false);
            pooledViews.Push(tile.View);
        }

        internal class Tile
        {
            public readonly AtlasChunk View;
            public readonly CancellationTokenSource Cts = new ();
            public string Url = string.Empty;
            public Texture2D? Texture;
            public Sprite? Sprite;
            public float? FailedAt;
            public float DistanceSqToCamera;
            public int LastUsed;
            public bool Pending;

            /// <summary>Read from the disk cache only, never downloaded, until a camera shows the tile.</summary>
            public bool CacheOnly;

            public Tile(AtlasChunk view)
            {
                View = view;
            }
        }

        private class NearestFirstComparer : IComparer<Vector3Int>
        {
            private readonly Dictionary<Vector3Int, Tile> tiles;

            public NearestFirstComparer(Dictionary<Vector3Int, Tile> tiles)
            {
                this.tiles = tiles;
            }

            public int Compare(Vector3Int a, Vector3Int b) =>
                DistanceSq(a).CompareTo(DistanceSq(b));

            // A tile dropped since it was queued sorts last, where the pump skips it.
            private float DistanceSq(Vector3Int id) =>
                tiles.TryGetValue(id, out Tile? tile) ? tile.DistanceSqToCamera : float.MaxValue;
        }
    }
}
