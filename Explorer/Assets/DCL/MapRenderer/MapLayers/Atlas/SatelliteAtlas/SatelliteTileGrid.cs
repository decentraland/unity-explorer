using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    /// <summary>
    ///     The satellite tile pyramid over the bundled grid. Level L splits the grid into 2^L x 2^L tiles of <see cref="TILE_PIXELS" /> px
    ///     (i eastward, j southward), so a <see cref="BASE_LEVEL" /> tile covers one bundled chunk.
    /// </summary>
    internal readonly struct SatelliteTileGrid
    {
        internal const int BASE_LEVEL = 3;
        private const int MAX_LEVEL = 8;
        private const int MAX_TILES_PER_CAMERA = 64;
        private const int TILE_PIXELS = 1024;
        private const float SHARPNESS_BIAS = 0.4f;

        private readonly Vector2 topLeft;
        private readonly float bundledChunkSize;

        public float BaseLevelPixelsPerUnit => TILE_PIXELS / bundledChunkSize;

        /// <param name="topLeft">The bundled grid's top-left corner, in local units.</param>
        /// <param name="bundledChunkSize">One bundled chunk's side, in local units.</param>
        public SatelliteTileGrid(Vector2 topLeft, float bundledChunkSize)
        {
            this.topLeft = topLeft;
            this.bundledChunkSize = bundledChunkSize;
        }

        /// <summary>
        ///     The level whose pixel density is nearest to <paramref name="screenPixelsPerUnit" /> on a log scale, with a bias toward the sharper level.
        ///     With the bias, a tile shows at 0.54x to 1.07x of its size. A magnified tile looks soft, and the next sharper level costs four times the bytes.
        /// </summary>
        public static int LevelFor(float screenPixelsPerUnit, float baseLevelPixelsPerUnit)
        {
            int level = BASE_LEVEL + Mathf.RoundToInt(Mathf.Log(screenPixelsPerUnit / baseLevelPixelsPerUnit, 2f) + SHARPNESS_BIAS);
            return Mathf.Clamp(level, BASE_LEVEL, MAX_LEVEL);
        }

        /// <summary>Half-open tile index range of <paramref name="level" /> that <paramref name="rect" /> overlaps, clamped to the grid.</summary>
        public RectInt TileRange(Rect rect, int level)
        {
            int side = 1 << level;
            float tileSize = TileSize(level);

            int minI = Mathf.Clamp(Mathf.FloorToInt((rect.xMin - topLeft.x) / tileSize), 0, side - 1);
            int maxI = Mathf.Clamp(Mathf.FloorToInt((rect.xMax - topLeft.x) / tileSize), 0, side - 1);
            int minJ = Mathf.Clamp(Mathf.FloorToInt((topLeft.y - rect.yMax) / tileSize), 0, side - 1);
            int maxJ = Mathf.Clamp(Mathf.FloorToInt((topLeft.y - rect.yMin) / tileSize), 0, side - 1);

            return new RectInt(minI, minJ, maxI - minI + 1, maxJ - minJ + 1);
        }

        /// <summary>
        ///     Steps <paramref name="level" /> down until <paramref name="rect" /> needs at most <see cref="MAX_TILES_PER_CAMERA" /> tiles.
        ///     The 8x8 tiles of the base level always fit. Outputs the tile range of the returned level.
        /// </summary>
        public int LevelWithinTileBudget(Rect rect, int level, out RectInt range)
        {
            range = TileRange(rect, level);

            while (level > BASE_LEVEL && range.width * range.height > MAX_TILES_PER_CAMERA)
            {
                level--;
                range = TileRange(rect, level);
            }

            return level;
        }

        /// <summary>Centre of the tile <paramref name="id" /> (x, y, level) in local units.</summary>
        public Vector2 TileCenter(Vector3Int id)
        {
            float tileSize = TileSize(id.z);
            return new Vector2(topLeft.x + ((id.x + 0.5f) * tileSize), topLeft.y - ((id.y + 0.5f) * tileSize));
        }

        /// <summary>Side of a tile of <paramref name="level" /> in local units.</summary>
        public float TileSize(int level) =>
            bundledChunkSize * (1 << BASE_LEVEL) / (1 << level);
    }
}
