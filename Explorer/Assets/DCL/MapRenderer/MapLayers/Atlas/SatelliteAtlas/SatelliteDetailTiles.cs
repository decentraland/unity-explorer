using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MapRenderer.Culling;
using DCL.WebRequests;
using DG.Tweening;
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
    ///     Each map camera gets the coarsest level that is at least as sharp as its render texture; finer levels draw on top.
    /// </summary>
    internal class SatelliteDetailTiles : IDisposable
    {
        internal const int BASE_LEVEL = 3;
        internal const int MIN_LEVEL = 4;
        internal const int MAX_LEVEL = 8;
        internal const int MAX_TILES_PER_CAMERA = 64;
        private const int TILE_PIXELS = 512;
        private const int MAX_CACHED_TILES = 64;
        private const float FADE_IN_SECONDS = 0.25f;
        private const float RETRY_FAILED_TILE_AFTER_SECONDS = 30f;
        private const float SATURATION_VALUE = 1f;
        private static readonly int SATURATION = Shader.PropertyToID("_Saturation");

        private static readonly IComparer<KeyValuePair<int, Vector3Int>> OLDEST_FIRST =
            Comparer<KeyValuePair<int, Vector3Int>>.Create(static (a, b) => a.Key.CompareTo(b.Key));

        private readonly string baseUrl;
        private readonly IWebRequestController webRequestController;
        private readonly IMapCullingController cullingController;
        private readonly AtlasChunk template;
        private readonly int drawOrderOfMinLevel;

        private readonly Dictionary<Vector3Int, Tile> tiles = new ();
        private readonly Stack<AtlasChunk> pooledViews = new ();
        private readonly List<KeyValuePair<int, Vector3Int>> evictions = new ();

        private Vector2 gridTopLeft;
        private float baseChunkSize;
        private int refreshStamp;
        private bool tileFailureReported;

        public SatelliteDetailTiles(string baseUrl, IWebRequestController webRequestController, IMapCullingController cullingController, SpriteRenderer template, int drawOrderOfMinLevel)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            this.webRequestController = webRequestController;
            this.cullingController = cullingController;
            this.drawOrderOfMinLevel = drawOrderOfMinLevel;

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

        public void Dispose()
        {
            cullingController.CamerasChanged -= Refresh;

            foreach (Tile tile in tiles.Values)
                DestroyTile(tile);

            tiles.Clear();

            while (pooledViews.Count > 0)
                UnityObjectUtils.SafeDestroy(pooledViews.Pop().gameObject);

            UnityObjectUtils.SafeDestroy(template.gameObject);
        }

        /// <summary>Loads the tiles every active camera shows. Does nothing while the satellite layer is hidden.</summary>
        internal void Refresh()
        {
            if (!template.transform.parent.gameObject.activeInHierarchy)
                return;

            refreshStamp++;
            float bundledPixelsPerUnit = TILE_PIXELS / baseChunkSize;

            IReadOnlyList<CameraState> cameras = cullingController.CameraStates;

            for (var c = 0; c < cameras.Count; c++)
            {
                CameraState camera = cameras[c];

                if (!camera.CameraController.Camera.isActiveAndEnabled)
                    continue;

                float screenPixelsPerUnit = camera.CameraController.GetRenderTexture().height / camera.Rect.height;
                int level = LevelFor(screenPixelsPerUnit, bundledPixelsPerUnit);

                if (level < MIN_LEVEL)
                    continue;

                level = LevelWithinTileBudget(camera.Rect, level, gridTopLeft, baseChunkSize, out RectInt range);

                for (int j = range.yMin; j < range.yMax; j++)
                for (int i = range.xMin; i < range.xMax; i++)
                    Use(new Vector3Int(i, j, level));
            }

            CollectEvictions(tiles, refreshStamp, MAX_CACHED_TILES, evictions);

            for (var k = 0; k < evictions.Count; k++)
                if (tiles.Remove(evictions[k].Value, out Tile? evicted))
                    DestroyTile(evicted);
        }

        /// <summary>The coarsest level whose tiles have at least <paramref name="screenPixelsPerUnit" />, or a level below <see cref="MIN_LEVEL" /> when the bundled chunks suffice.</summary>
        internal static int LevelFor(float screenPixelsPerUnit, float bundledPixelsPerUnit)
        {
            if (screenPixelsPerUnit <= bundledPixelsPerUnit)
                return BASE_LEVEL;

            int level = BASE_LEVEL + Mathf.CeilToInt(Mathf.Log(screenPixelsPerUnit / bundledPixelsPerUnit, 2f));
            return Mathf.Min(level, MAX_LEVEL);
        }

        /// <summary>Half-open tile index range of <paramref name="level" /> that <paramref name="rect" /> overlaps, clamped to the grid.</summary>
        internal static RectInt TileRange(Rect rect, int level, Vector2 gridTopLeft, float bundledChunkSize)
        {
            int side = 1 << level;
            float tileSize = bundledChunkSize * (1 << BASE_LEVEL) / side;

            int minI = Mathf.Clamp(Mathf.FloorToInt((rect.xMin - gridTopLeft.x) / tileSize), 0, side - 1);
            int maxI = Mathf.Clamp(Mathf.FloorToInt((rect.xMax - gridTopLeft.x) / tileSize), 0, side - 1);
            int minJ = Mathf.Clamp(Mathf.FloorToInt((gridTopLeft.y - rect.yMax) / tileSize), 0, side - 1);
            int maxJ = Mathf.Clamp(Mathf.FloorToInt((gridTopLeft.y - rect.yMin) / tileSize), 0, side - 1);

            return new RectInt(minI, minJ, maxI - minI + 1, maxJ - minJ + 1);
        }

        /// <summary>
        ///     Steps <paramref name="level" /> down until <paramref name="rect" /> needs at most <see cref="MAX_TILES_PER_CAMERA" /> tiles,
        ///     stopping at <see cref="MIN_LEVEL" />. Outputs the tile range of the returned level.
        /// </summary>
        internal static int LevelWithinTileBudget(Rect rect, int level, Vector2 gridTopLeft, float bundledChunkSize, out RectInt range)
        {
            range = TileRange(rect, level, gridTopLeft, bundledChunkSize);

            while (level > MIN_LEVEL && range.width * range.height > MAX_TILES_PER_CAMERA)
            {
                level--;
                range = TileRange(rect, level, gridTopLeft, bundledChunkSize);
            }

            return level;
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

        private void Use(Vector3Int id)
        {
            if (!tiles.TryGetValue(id, out Tile? tile))
            {
                tile = new Tile(RentView());
                tiles.Add(id, tile);
                LoadAsync(id, tile).Forget();
            }
            else if (tile.FailedAt is { } failedAt && UnityEngine.Time.realtimeSinceStartup - failedAt >= RETRY_FAILED_TILE_AFTER_SECONDS)
            {
                tile.FailedAt = null;
                LoadAsync(id, tile).Forget();
            }

            tile.LastUsed = refreshStamp;
        }

        private async UniTaskVoid LoadAsync(Vector3Int id, Tile tile)
        {
            CancellationToken ct = tile.Cts.Token;
            int level = id.z;
            var url = $"{baseUrl}/{level}/{id.x}%2C{id.y}.ktx2";
            Texture2D texture;

            try
            {
                texture = await webRequestController.GetTextureAsync(
                    new CommonArguments(URLAddress.FromString(url), RetryPolicy.WithRetries(1)),
                    // The tiles are already KTX2, so the request skips the media converter.
                    new GetTextureArguments(TextureType.Albedo, useKtx: false),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp, FilterMode.Bilinear),
                    ct,
                    ReportCategory.UI,
                    suppressErrors: true);

                await UniTask.SwitchToMainThread();
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                // One report per session: a wrong or offline URL fails every tile in view.
                if (!tileFailureReported)
                {
                    tileFailureReported = true;
                    ReportHub.LogException(e, ReportCategory.UI);
                }

                tile.FailedAt = UnityEngine.Time.realtimeSinceStartup;
                return;
            }

            // The tile was evicted while downloading: it no longer owns anything, so the texture is dropped here.
            if (ct.IsCancellationRequested)
            {
                UnityObjectUtils.SafeDestroy(texture);
                return;
            }

            // The texture is only drawn from now on, so its CPU copy is released.
            texture.Apply(false, true);

            tile.Texture = texture;
            float tileSize = baseChunkSize * (1 << BASE_LEVEL) / (1 << level);

            tile.Sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, texture.width / tileSize, 0, SpriteMeshType.FullRect, Vector4.one, false);

            SpriteRenderer renderer = tile.View.MainSpriteRenderer;
            renderer.sprite = tile.Sprite;
            renderer.sortingOrder = drawOrderOfMinLevel + level - MIN_LEVEL;
            renderer.color = AtlasChunkConstants.INITIAL_COLOR;
            renderer.enabled = true;

            tile.View.transform.localPosition = new Vector3(gridTopLeft.x + ((id.x + 0.5f) * tileSize), gridTopLeft.y - ((id.y + 0.5f) * tileSize), 0);
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

        internal class Tile
        {
            public readonly AtlasChunk View;
            public readonly CancellationTokenSource Cts = new ();
            public Texture2D? Texture;
            public Sprite? Sprite;
            public float? FailedAt;
            public int LastUsed;

            public Tile(AtlasChunk view)
            {
                View = view;
            }
        }
    }
}
