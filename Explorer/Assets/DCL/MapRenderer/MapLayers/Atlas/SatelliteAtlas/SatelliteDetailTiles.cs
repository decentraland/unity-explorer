using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MapRenderer.Culling;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    /// <summary>
    ///     Streams the satellite tiles over the bundled chunks. The bundled chunks stay underneath while a tile loads or after a tile fails.
    /// </summary>
    internal class SatelliteDetailTiles : IDisposable
    {
        internal const int MAX_CONCURRENT_LOADS = 8;

        // A failed tile is requested again after this wait, also when no camera moves.
        internal const float RETRY_FAILED_TILE_AFTER_SECONDS = 30f;

        // The number of finer tiles out of view that stay loaded. When a camera is released, no tile out of view stays loaded.
        private const int MAX_CACHED_TILES = 16;

        private const float SETTLE_SECONDS = 0.1f;

        private static readonly IComparer<KeyValuePair<int, Vector3Int>> OLDEST_FIRST =
            Comparer<KeyValuePair<int, Vector3Int>>.Create(static (a, b) => a.Key.CompareTo(b.Key));

        // Tests replace the clock, so that they do not wait out the retry delay.
        internal Func<float>? clockOverride;

        private readonly string baseUrl;
        private readonly IWebRequestController webRequestController;
        private readonly ISatelliteTileLoader loader;
        private readonly IMapCullingController cullingController;
        private readonly SatelliteTileViews views;
        private readonly CancellationTokenSource lifetimeCts = new ();

        private readonly Dictionary<Vector3Int, Tile> tiles = new ();
        private readonly List<Vector3Int> pending = new ();
        private readonly NearestFirstComparer nearestFirst;
        private readonly List<Vector3Int> unusedLoads = new ();
        private readonly List<KeyValuePair<int, Vector3Int>> evictions = new ();

        // These outlive the tiles, because a tile out of view is removed from tiles.
        private readonly HashSet<string> emptyUrls = new ();
        private readonly Dictionary<string, float> failedUrls = new ();

        // A completion source takes every awaiter while the load is pending; a preserved task takes only one until it completes.
        private readonly UniTaskCompletionSource<SatelliteManifest> manifestLoad = new ();

        private SatelliteTileGrid grid;
        private int refreshStamp;
        private int cameraCount;
        private float lastCameraChangeTime;
        private float lastFailureTime;
        private int activeLoads;
        private bool settlePending;
        private bool pumping;
        private bool retryPending;
        private bool tileFailureReported;

        internal int ActiveLoads => activeLoads;

        private float Now => clockOverride?.Invoke() ?? UnityEngine.Time.realtimeSinceStartup;

        public SatelliteDetailTiles(string baseUrl, IWebRequestController webRequestController, ISatelliteTileLoader loader, IMapCullingController cullingController,
            SatelliteTileViews views)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            this.webRequestController = webRequestController;
            this.loader = loader;
            this.cullingController = cullingController;
            this.views = views;
            nearestFirst = new NearestFirstComparer(tiles);
        }

        /// <summary>Both arguments are in the local units of the map.</summary>
        public void Initialize(Vector2 bundledGridTopLeft, float bundledChunkSize)
        {
            grid = new SatelliteTileGrid(bundledGridTopLeft, bundledChunkSize);

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
            views.Dispose();
            loader.Dispose();
        }

        internal void Refresh()
        {
            if (!views.LayerVisible)
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
        ///     Releases every tile finer than the base level, also the tiles that still load. <see cref="Refresh" /> does not release them while the
        ///     layer is hidden.
        /// </summary>
        internal void ReleaseFinerTiles()
        {
            // After a new stamp, no tile counts as used, so the eviction takes every finer tile.
            refreshStamp++;
            CollectEvictions(tiles, refreshStamp, 0, evictions);

            for (var k = 0; k < evictions.Count; k++)
                if (tiles.Remove(evictions[k].Value, out Tile? evicted))
                    ReleaseTile(evicted);
        }

        internal static void CollectUnusedLoads(Dictionary<Vector3Int, Tile> tiles, int refreshStamp, List<Vector3Int> result)
        {
            result.Clear();

            foreach ((Vector3Int id, Tile tile) in tiles)
                if (id.z != SatelliteTileGrid.BASE_LEVEL && tile.Texture == null && tile.LastUsed != refreshStamp)
                    result.Add(id);
        }

        /// <summary>
        ///     Collects the finer tiles to release, oldest first, to get back to <paramref name="maxCachedTiles" /> finer tiles.
        ///     Tiles in view are never collected.
        /// </summary>
        internal static void CollectEvictions(Dictionary<Vector3Int, Tile> tiles, int refreshStamp, int maxCachedTiles, List<KeyValuePair<int, Vector3Int>> result)
        {
            result.Clear();

            int excess = -maxCachedTiles;

            foreach (Vector3Int id in tiles.Keys)
                if (id.z != SatelliteTileGrid.BASE_LEVEL)
                    excess++;

            if (excess <= 0)
                return;

            foreach ((Vector3Int id, Tile tile) in tiles)
                if (id.z != SatelliteTileGrid.BASE_LEVEL && tile.LastUsed != refreshStamp)
                    result.Add(new KeyValuePair<int, Vector3Int>(tile.LastUsed, id));

            result.Sort(OLDEST_FIRST);

            if (result.Count > excess)
                result.RemoveRange(excess, result.Count - excess);
        }

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

                if (views.LayerVisible)
                {
                    VisitVisibleTiles(manifest);
                    PumpLoads();
                }
            }
            catch (OperationCanceledException) { }
            finally { settlePending = false; }
        }

        /// <summary>
        ///     Queues the base-level tiles to read from the disk cache only. A tile that is not in the cache downloads only when a camera shows it.
        /// </summary>
        private async UniTaskVoid LoadCachedBaseLevelAsync(CancellationToken ct)
        {
            SatelliteManifest manifest;

            try { manifest = await manifestLoad.Task; }
            catch (OperationCanceledException) { return; }

            if (ct.IsCancellationRequested)
                return;

            const int SIDE = 1 << SatelliteTileGrid.BASE_LEVEL;

            for (var j = 0; j < SIDE; j++)
            for (var i = 0; i < SIDE; i++)
            {
                var id = new Vector3Int(i, j, SatelliteTileGrid.BASE_LEVEL);

                if (tiles.ContainsKey(id) || !manifest.TryGetTilePath(id, out string path))
                    continue;

                var tile = new Tile(views.Rent()) { Url = $"{baseUrl}/{path}", CacheOnly = true, Pending = true };

                // Sorts after every tile that a camera shows.
                tile.DistanceSqToCamera = float.MaxValue;
                tiles.Add(id, tile);
                pending.Add(id);
            }

            PumpLoads();
        }

        /// <summary>Marks the tiles in view as used. With a manifest, the method also queues the missing tiles.</summary>
        private void VisitVisibleTiles(SatelliteManifest? requestWith)
        {
            IReadOnlyList<CameraState> cameras = cullingController.CameraStates;

            for (var c = 0; c < cameras.Count; c++)
            {
                CameraState camera = cameras[c];

                if (!camera.CameraController.Camera.isActiveAndEnabled)
                    continue;

                Rect rect = camera.Rect;
                Vector2 center = rect.center;

                UseRange(grid.TileRange(rect, SatelliteTileGrid.BASE_LEVEL), SatelliteTileGrid.BASE_LEVEL, requestWith, center);

                float screenPixelsPerUnit = camera.CameraController.GetRenderTexture().height / rect.height;
                int level = SatelliteTileGrid.LevelFor(screenPixelsPerUnit, grid.BaseLevelPixelsPerUnit);

                if (level == SatelliteTileGrid.BASE_LEVEL)
                    continue;

                level = grid.LevelWithinTileBudget(rect, level, out RectInt range);

                if (level > SatelliteTileGrid.BASE_LEVEL)
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

                if (requestWith != null && (tile.Pending || tile.CacheOnly))
                {
                    tile.CacheOnly = false;
                    tile.DistanceSqToCamera = (grid.TileCenter(id) - cameraCenter).sqrMagnitude;
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

            if (!requestWith.TryGetTilePath(id, out string path))
                return;

            var url = $"{baseUrl}/{path}";

            if (emptyUrls.Contains(url))
                return;

            tile = new Tile(views.Rent()) { LastUsed = refreshStamp, Url = url };
            tiles.Add(id, tile);

            // A failed tile that returns to view before its retry wait ends waits for the rest of that wait.
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
            tile.DistanceSqToCamera = (grid.TileCenter(id) - cameraCenter).sqrMagnitude;
            pending.Add(id);
        }

        private void PumpLoads()
        {
            // A load that completes synchronously calls PumpLoads from inside this loop. The loop already reuses the slot that the load frees.
            if (pumping || pending.Count == 0)
                return;

            pumping = true;

            try
            {
                pending.Sort(nearestFirst);
                var consumed = 0;

                for (; consumed < pending.Count && activeLoads < MAX_CONCURRENT_LOADS; consumed++)
                {
                    // The tile can be gone, or a duplicate entry can have started it already.
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
            Texture2D? texture = null;
            Exception? failure = null;

            try { texture = await loader.LoadAsync(tile.Url, tile.CacheOnly, ct); }
            catch (OperationCanceledException) { }
            catch (Exception e) { failure = e; }

            // A load can end on another thread. The tiles and the views change only on the main thread.
            await UniTask.SwitchToMainThread();
            activeLoads--;

            // The tile was released while it loaded, so the texture has no owner.
            if (ct.IsCancellationRequested)
                UnityObjectUtils.SafeDestroy(texture);
            else if (texture != null)
            {
                tile.Texture = texture;
                tile.Sprite = views.Show(tile.View, texture, id, grid);
            }

            // A cache-only tile that the cache did not hold, or could not decode, waits until a camera shows it.
            else if (tile.CacheOnly)
            {
                tiles.Remove(id);
                ReleaseTile(tile);
            }
            else if (failure != null)
                Fail(id, tile, failure);

            // A camera showed the tile while the disk read missed, so the tile is queued for download.
            else
            {
                tile.Pending = true;
                pending.Add(id);
            }

            PumpLoads();
        }

        private async UniTaskVoid PublishManifestAsync(CancellationToken ct)
        {
            try { manifestLoad.TrySetResult(await SatelliteManifest.LoadAsync(webRequestController, baseUrl, ct)); }
            catch (OperationCanceledException) { manifestLoad.TrySetCanceled(ct); }
        }

        private void Fail(Vector3Int id, Tile tile, Exception failure)
        {
            // The server does not have the tile, so the tile stays empty for the session. Nothing is retried or reported.
            if (failure is UnityWebRequestException { ResponseCode: WebRequestUtils.NOT_FOUND })
            {
                emptyUrls.Add(tile.Url);
                tiles.Remove(id);
                ReleaseTile(tile);
                return;
            }

            // Report only once, because a wrong or offline URL fails every tile in view.
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
        ///     Without this refresh, a failed tile under a camera that does not move is never requested again.
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

        private void ReleaseTile(Tile tile)
        {
            tile.Cts.SafeCancelAndDispose();
            UnityObjectUtils.SafeDestroy(tile.Sprite);
            UnityObjectUtils.SafeDestroy(tile.Texture);
            views.Return(tile.View);
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

            // Read only from the disk cache, never downloaded, until a camera shows the tile.
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

            // A tile that was removed after it was queued sorts last.
            private float DistanceSq(Vector3Int id) =>
                tiles.TryGetValue(id, out Tile? tile) ? tile.DistanceSqToCamera : float.MaxValue;
        }
    }
}
