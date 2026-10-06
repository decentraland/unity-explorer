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
    ///     Streams the satellite zoom levels finer than the bundled 8x8 chunks from <c>{baseUrl}/{level}/{i},{j}.ktx2</c>.
    ///     Level L splits the bundled grid into 2^L x 2^L tiles (i eastward, j southward), so level 3 is the bundled chunks.
    ///     Each map camera gets the level whose sharpness is nearest to its render texture's; finer levels draw on top.
    ///     Tiles are requested once the cameras have been still for <see cref="SETTLE_SECONDS" />, so a zoom tween or a pan
    ///     doesn't fetch levels and tiles that only pass through the view; a download whose tile leaves the view is cancelled.
    ///     At most <see cref="MAX_CONCURRENT_LOADS" /> tiles load at a time, nearest to their camera's centre first.
    ///     A tile that fails is requested again after <see cref="RETRY_FAILED_TILE_AFTER_SECONDS" />, even if no camera moves.
    ///     Downloaded tiles are kept in the disk cache, so a later session reads them from disk.
    ///     A world streams its own tiles from <c>{baseUrl}/worlds/{name}</c> on the same grid. It has no bundled chunks, so its
    ///     coarsest level is shown at every zoom, and only the tiles over its parcels are requested.
    /// </summary>
    internal class SatelliteDetailTiles : IDisposable
    {
        internal const int BASE_LEVEL = 3;
        internal const int MIN_LEVEL = 4;
        internal const int MAX_LEVEL = 8;
        internal const int MAX_TILES_PER_CAMERA = 64;
        internal const int MAX_CONCURRENT_LOADS = 8;
        private const int TILE_PIXELS = 512;
        private const string CACHE_EXTENSION = "ktx2";
        private const int CACHE_ITERATION = 1;
        private const int MAX_CACHED_TILES = 64;
        private const float SETTLE_SECONDS = 0.1f;
        private const float FADE_IN_SECONDS = 0.25f;
        private const float RETRY_FAILED_TILE_AFTER_SECONDS = 30f;
        private const float SATURATION_VALUE = 1f;
        private static readonly int SATURATION = Shader.PropertyToID("_Saturation");

        private static readonly IComparer<KeyValuePair<int, Vector3Int>> OLDEST_FIRST =
            Comparer<KeyValuePair<int, Vector3Int>>.Create(static (a, b) => a.Key.CompareTo(b.Key));

        private readonly string genesisBaseUrl;
        private readonly IWebRequestController webRequestController;
        private readonly IDiskCache<byte[]> diskCache;
        private readonly IMapCullingController cullingController;
        private readonly AtlasChunk template;
        private readonly int drawOrderOfMinLevel;

        private readonly Dictionary<Vector3Int, Tile> tiles = new ();
        private readonly Stack<AtlasChunk> pooledViews = new ();
        private readonly List<Vector3Int> pending = new ();
        private readonly NearestFirstComparer nearestFirst;
        private readonly List<Vector3Int> unusedLoads = new ();
        private readonly List<KeyValuePair<int, Vector3Int>> evictions = new ();

        private CancellationTokenSource lifetimeCts = new ();
        private Source source;
        private Vector2 gridTopLeft;
        private float baseChunkSize;
        private int refreshStamp;
        private float lastCameraChangeTime;
        private float lastFailureTime;
        private int activeLoads;
        private bool settlePending;
        private bool retryPending;
        private bool tileFailureReported;
        private bool diskCacheFailureReported;

        public SatelliteDetailTiles(string baseUrl, IWebRequestController webRequestController, IDiskCache<byte[]> diskCache, IMapCullingController cullingController, SpriteRenderer template, int drawOrderOfMinLevel)
        {
            genesisBaseUrl = baseUrl.TrimEnd('/');
            source = Source.GenesisCity(genesisBaseUrl);
            this.webRequestController = webRequestController;
            this.diskCache = diskCache;
            this.cullingController = cullingController;
            this.drawOrderOfMinLevel = drawOrderOfMinLevel;
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

            cullingController.CamerasChanged += Refresh;
            Refresh();
        }

        /// <summary>Streams the tiles of the world <paramref name="worldName" />, only inside <paramref name="localBounds" /> when they are known.</summary>
        public void ShowWorld(string worldName, Rect? localBounds) =>
            SetSource(Source.World($"{genesisBaseUrl}/worlds/{Uri.EscapeDataString(worldName.ToLowerInvariant())}", localBounds));

        public void ShowGenesisCity() =>
            SetSource(Source.GenesisCity(genesisBaseUrl));

        public void Dispose()
        {
            cullingController.CamerasChanged -= Refresh;
            lifetimeCts.SafeCancelAndDispose();
            pending.Clear();

            foreach (Tile tile in tiles.Values)
                DestroyTile(tile);

            tiles.Clear();

            while (pooledViews.Count > 0)
                UnityObjectUtils.SafeDestroy(pooledViews.Pop().gameObject);

            UnityObjectUtils.SafeDestroy(template.gameObject);
        }

        /// <summary>Drops every tile and in-flight load of the previous source, then requests the new source's tiles in view.</summary>
        private void SetSource(Source newSource)
        {
            if (newSource.Equals(source))
                return;

            source = newSource;

            // Cancelling the lifetime also ends the pending settle wait, so the refresh below schedules a new one.
            lifetimeCts.SafeCancelAndDispose();
            lifetimeCts = new CancellationTokenSource();
            settlePending = false;
            retryPending = false;
            pending.Clear();

            foreach (Tile tile in tiles.Values)
                DestroyTile(tile);

            tiles.Clear();
            Refresh();
        }

        /// <summary>
        ///     Marks the tiles every active camera shows, drops the loads of tiles that left the view, evicts the oldest
        ///     loaded tiles over the cache cap, and schedules the missing tiles' requests for when the cameras settle.
        ///     Does nothing while the satellite layer is hidden.
        /// </summary>
        internal void Refresh()
        {
            if (!template.transform.parent.gameObject.activeInHierarchy)
                return;

            refreshStamp++;
            lastCameraChangeTime = UnityEngine.Time.realtimeSinceStartup;

            VisitVisibleTiles(request: false);

            CollectUnusedLoads(tiles, refreshStamp, unusedLoads);

            for (var k = 0; k < unusedLoads.Count; k++)
                if (tiles.Remove(unusedLoads[k], out Tile? dropped))
                    DestroyTile(dropped);

            CollectEvictions(tiles, refreshStamp, MAX_CACHED_TILES, evictions);

            for (var k = 0; k < evictions.Count; k++)
                if (tiles.Remove(evictions[k].Value, out Tile? evicted))
                    DestroyTile(evicted);

            if (!settlePending)
                RequestWhenSettledAsync(lifetimeCts.Token).Forget();
        }

        /// <summary>
        ///     The level whose pixel density is nearest to <paramref name="screenPixelsPerUnit" /> on a log scale, so a tile is shown
        ///     between 0.71x and 1.41x its size: the next sharper level would cost four times the bytes for the same view.
        ///     Returns a level below <see cref="MIN_LEVEL" /> when the bundled chunks suffice.
        /// </summary>
        internal static int LevelFor(float screenPixelsPerUnit, float bundledPixelsPerUnit)
        {
            if (screenPixelsPerUnit <= bundledPixelsPerUnit)
                return BASE_LEVEL;

            int level = BASE_LEVEL + Mathf.RoundToInt(Mathf.Log(screenPixelsPerUnit / bundledPixelsPerUnit, 2f));
            return Mathf.Min(level, MAX_LEVEL);
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
        ///     stopping at <paramref name="minLevel" />. Outputs the tile range of the returned level.
        /// </summary>
        internal static int LevelWithinTileBudget(Rect rect, int level, int minLevel, Vector2 gridTopLeft, float bundledChunkSize, out RectInt range)
        {
            range = TileRange(rect, level, gridTopLeft, bundledChunkSize);

            while (level > minLevel && range.width * range.height > MAX_TILES_PER_CAMERA)
            {
                level--;
                range = TileRange(rect, level, gridTopLeft, bundledChunkSize);
            }

            return level;
        }

        /// <summary>
        ///     Shrinks <paramref name="range" /> to at most <paramref name="maxTiles" /> tiles around <paramref name="centerTile" />,
        ///     cutting the longer side first. Used at a minimum level that has no coarser level to step down to.
        /// </summary>
        internal static RectInt CapRange(RectInt range, Vector2Int centerTile, int maxTiles)
        {
            if (range.width * range.height <= maxTiles)
                return range;

            int side = Mathf.FloorToInt(Mathf.Sqrt(maxTiles));
            int width = Mathf.Min(range.width, Mathf.Max(side, maxTiles / range.height));
            int height = Mathf.Min(range.height, maxTiles / width);

            int x = Mathf.Clamp(centerTile.x - (width / 2), range.xMin, range.xMax - width);
            int y = Mathf.Clamp(centerTile.y - (height / 2), range.yMin, range.yMax - height);

            return new RectInt(x, y, width, height);
        }

        /// <summary>The part of <paramref name="rect" /> inside <paramref name="bounds" />; false when they don't overlap.</summary>
        internal static bool TryClip(Rect rect, Rect bounds, out Rect clipped)
        {
            clipped = Rect.MinMaxRect(Mathf.Max(rect.xMin, bounds.xMin), Mathf.Max(rect.yMin, bounds.yMin),
                Mathf.Min(rect.xMax, bounds.xMax), Mathf.Min(rect.yMax, bounds.yMax));

            return clipped.width > 0 && clipped.height > 0;
        }

        /// <summary>Index (x, y) of the tile of <paramref name="level" /> under <paramref name="position" />, unclamped.</summary>
        internal static Vector2Int TileIndex(Vector2 position, int level, Vector2 gridTopLeft, float bundledChunkSize)
        {
            float tileSize = TileSize(level, bundledChunkSize);
            return new Vector2Int(Mathf.FloorToInt((position.x - gridTopLeft.x) / tileSize), Mathf.FloorToInt((gridTopLeft.y - position.y) / tileSize));
        }

        /// <summary>Centre of the tile <paramref name="id" /> (x, y, level) in local units.</summary>
        internal static Vector2 TileCenter(Vector3Int id, Vector2 gridTopLeft, float bundledChunkSize)
        {
            float tileSize = TileSize(id.z, bundledChunkSize);
            return new Vector2(gridTopLeft.x + ((id.x + 0.5f) * tileSize), gridTopLeft.y - ((id.y + 0.5f) * tileSize));
        }

        /// <summary>
        ///     Fills <paramref name="result" /> with the tiles that have no texture and were not used in the refresh
        ///     <paramref name="refreshStamp" />: pending or downloading tiles that left the view, and failed or empty tiles out of view.
        /// </summary>
        internal static void CollectUnusedLoads(Dictionary<Vector3Int, Tile> tiles, int refreshStamp, List<Vector3Int> result)
        {
            result.Clear();

            foreach ((Vector3Int id, Tile tile) in tiles)
                if (tile.Texture == null && tile.LastUsed != refreshStamp)
                    result.Add(id);
        }

        /// <summary>
        ///     Fills <paramref name="result" /> with the tiles to evict to get back to <paramref name="maxCachedTiles" />, oldest first.
        ///     Tiles used in the refresh <paramref name="refreshStamp" /> are never evicted.
        /// </summary>
        internal static void CollectEvictions(Dictionary<Vector3Int, Tile> tiles, int refreshStamp, int maxCachedTiles, List<KeyValuePair<int, Vector3Int>> result)
        {
            result.Clear();

            int excess = tiles.Count - maxCachedTiles;

            if (excess <= 0)
                return;

            foreach ((Vector3Int id, Tile tile) in tiles)
                if (tile.LastUsed != refreshStamp)
                    result.Add(new KeyValuePair<int, Vector3Int>(tile.LastUsed, id));

            result.Sort(OLDEST_FIRST);

            if (result.Count > excess)
                result.RemoveRange(excess, result.Count - excess);
        }

        private static float TileSize(int level, float bundledChunkSize) =>
            bundledChunkSize * (1 << BASE_LEVEL) / (1 << level);

        /// <summary>Waits until no camera has changed for <see cref="SETTLE_SECONDS" />, then queues the visible tiles that are missing.</summary>
        private async UniTaskVoid RequestWhenSettledAsync(CancellationToken ct)
        {
            settlePending = true;

            try
            {
                while (true)
                {
                    float remaining = SETTLE_SECONDS - (UnityEngine.Time.realtimeSinceStartup - lastCameraChangeTime);

                    if (remaining <= 0f)
                        break;

                    await UniTask.Delay(TimeSpan.FromSeconds(remaining), ignoreTimeScale: true, cancellationToken: ct);
                }

                if (template.transform.parent.gameObject.activeInHierarchy)
                {
                    VisitVisibleTiles(request: true);
                    PumpLoads();
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                // A wait cancelled by a source change may already have been replaced by the new source's.
                if (!ct.IsCancellationRequested)
                    settlePending = false;
            }
        }

        /// <summary>Marks every tile inside an active camera as used in the current refresh and, when <paramref name="request" />, queues the missing ones.</summary>
        private void VisitVisibleTiles(bool request)
        {
            float bundledPixelsPerUnit = TILE_PIXELS / baseChunkSize;
            IReadOnlyList<CameraState> cameras = cullingController.CameraStates;

            for (var c = 0; c < cameras.Count; c++)
            {
                CameraState camera = cameras[c];

                if (!camera.CameraController.Camera.isActiveAndEnabled)
                    continue;

                Rect rect = camera.Rect;

                if (source.Bounds is { } bounds && !TryClip(rect, bounds, out rect))
                    continue;

                float screenPixelsPerUnit = camera.CameraController.GetRenderTexture().height / camera.Rect.height;
                int level = Mathf.Min(LevelFor(screenPixelsPerUnit, bundledPixelsPerUnit), source.MaxLevel);

                if (level < source.MinLevel)
                {
                    if (source.HasBundledChunks)
                        continue;

                    level = source.MinLevel;
                }

                level = LevelWithinTileBudget(rect, level, source.MinLevel, gridTopLeft, baseChunkSize, out RectInt range);

                if (!source.HasBundledChunks)
                    range = CapRange(range, TileIndex(camera.Rect.center, level, gridTopLeft, baseChunkSize), MAX_TILES_PER_CAMERA);

                for (int j = range.yMin; j < range.yMax; j++)
                for (int i = range.xMin; i < range.xMax; i++)
                    Use(new Vector3Int(i, j, level), request, camera.Rect.center);
            }
        }

        private void Use(Vector3Int id, bool request, Vector2 cameraCenter)
        {
            if (tiles.TryGetValue(id, out Tile? tile))
            {
                tile.LastUsed = refreshStamp;

                if (request && tile.FailedAt is { } failedAt && UnityEngine.Time.realtimeSinceStartup - failedAt >= RETRY_FAILED_TILE_AFTER_SECONDS)
                {
                    tile.FailedAt = null;
                    Enqueue(id, tile, cameraCenter);
                }

                return;
            }

            if (!request)
                return;

            tile = new Tile(RentView()) { LastUsed = refreshStamp };
            tiles.Add(id, tile);
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
            if (pending.Count == 0)
                return;

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

        private async UniTaskVoid LoadAsync(Vector3Int id, Tile tile)
        {
            CancellationToken ct = tile.Cts.Token;
            var url = $"{source.BaseUrl}/{id.z}/{id.x}%2C{id.y}.ktx2";
            Texture2D? texture = null;
            Exception? failure = null;

            try
            {
                byte[] bytes = await FetchAsync(url, ct);

                await UniTask.SwitchToMainThread(ct);
                texture = await KtxTextureDecoder.DecodeAsync(bytes, linear: false, TextureWrapMode.Clamp, FilterMode.Bilinear, readable: false, url);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { failure = e; }

            // A request can fail on another thread; the tiles and their views are only touched on the main one.
            await UniTask.SwitchToMainThread();
            activeLoads--;

            // The tile was dropped while loading: it no longer owns anything, so the texture is destroyed here.
            if (ct.IsCancellationRequested)
                UnityObjectUtils.SafeDestroy(texture);
            else if (failure != null)
                Fail(tile, failure);
            else if (texture != null)
                Show(id, tile, texture);

            PumpLoads();
        }

        /// <summary>The tile's KTX2 file from the disk cache, or downloaded and stored there for the next session.</summary>
        private async UniTask<byte[]> FetchAsync(string url, CancellationToken ct)
        {
            EnumResult<Option<byte[]>, TaskError> cached;

            using (HashKey key = NewCacheKey(url))
                cached = await diskCache.ContentAsync(key, CACHE_EXTENSION, ct);

            ct.ThrowIfCancellationRequested();

            if (cached.Success)
            {
                if (cached.Value.Has)
                    return cached.Value.Value;
            }
            else
                ReportDiskCacheFailure(cached.Error!.Value.Message);

            byte[] bytes = await webRequestController
                                .GetAsync(new CommonArguments(URLAddress.FromString(url), RetryPolicy.WithRetries(1)), ct, ReportCategory.UI, suppressErrors: true)
                                .GetDataCopyAsync();

            // The write runs on the streamer's lifetime: the tile can be dropped before it ends, and the bytes are already in memory.
            StoreAsync(url, bytes, lifetimeCts.Token).Forget();
            return bytes;
        }

        private async UniTaskVoid StoreAsync(string url, byte[] bytes, CancellationToken ct)
        {
            try
            {
                using HashKey key = NewCacheKey(url);
                EnumResult<TaskError> result = await diskCache.PutAsync(key, CACHE_EXTENSION, bytes, ct);

                if (!result.Success)
                    ReportDiskCacheFailure(result.Error!.Value.Message);
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

        private void Fail(Tile tile, Exception failure)
        {
            // A tile the server doesn't have stays empty: there is nothing to retry or report.
            if (failure is UnityWebRequestException { ResponseCode: WebRequestUtils.NOT_FOUND })
            {
                tile.Empty = true;
                return;
            }

            // One report per session: a wrong or offline URL fails every tile in view.
            if (!tileFailureReported)
            {
                tileFailureReported = true;
                ReportHub.LogException(failure, ReportCategory.UI);
            }

            tile.FailedAt = lastFailureTime = UnityEngine.Time.realtimeSinceStartup;

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
                float remaining = RETRY_FAILED_TILE_AFTER_SECONDS - (UnityEngine.Time.realtimeSinceStartup - lastFailureTime);

                if (remaining <= 0f)
                    break;

                // A source change resets the flag for the new source's failures.
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
            renderer.sortingOrder = drawOrderOfMinLevel + id.z - MIN_LEVEL;
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

        private void DestroyTile(Tile tile)
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

        /// <summary>Where the tiles come from, which levels exist there and which part of the grid they cover.</summary>
        private readonly struct Source : IEquatable<Source>
        {
            public readonly string BaseUrl;
            public readonly int MinLevel;
            public readonly int MaxLevel;
            public readonly Rect? Bounds;

            /// <summary>Genesis City's bundled chunks show the zooms coarser than <see cref="MinLevel" />; a world has none.</summary>
            public readonly bool HasBundledChunks;

            private Source(string baseUrl, int minLevel, int maxLevel, Rect? bounds, bool hasBundledChunks)
            {
                BaseUrl = baseUrl;
                MinLevel = minLevel;
                MaxLevel = maxLevel;
                Bounds = bounds;
                HasBundledChunks = hasBundledChunks;
            }

            public static Source GenesisCity(string baseUrl) =>
                new (baseUrl, MIN_LEVEL, MAX_LEVEL, null, true);

            // Worlds are captured at the coarsest streamed level only.
            public static Source World(string baseUrl, Rect? bounds) =>
                new (baseUrl, MIN_LEVEL, MIN_LEVEL, bounds, false);

            public bool Equals(Source other) =>
                BaseUrl == other.BaseUrl && MinLevel == other.MinLevel && MaxLevel == other.MaxLevel && Nullable.Equals(Bounds, other.Bounds)
                && HasBundledChunks == other.HasBundledChunks;

            public override bool Equals(object? obj) =>
                obj is Source other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(BaseUrl, MinLevel, MaxLevel, Bounds, HasBundledChunks);
        }

        internal class Tile
        {
            public readonly AtlasChunk View;
            public readonly CancellationTokenSource Cts = new ();
            public Texture2D? Texture;
            public Sprite? Sprite;
            public float? FailedAt;
            public float DistanceSqToCamera;
            public int LastUsed;
            public bool Pending;
            public bool Empty;

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
