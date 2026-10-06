using Global.AppArgs;
using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Global.MapCapture
{
    /// <summary>Launch-argument configuration of one capture run (see <see cref="AppArgsFlags.MapCapture" />).</summary>
    public readonly struct MapCaptureArgs
    {
        /// <summary>Side cap of any single render target.</summary>
        public const int MAX_RENDER_PIXELS = 8192;

        private const int DEFAULT_BLOCK_SIZE = 8;
        private const int MAX_BLOCK_SIZE = 40;
        private const int DEFAULT_PIXELS_PER_PARCEL = 256;
        private const int MIN_PIXELS_PER_PARCEL = 16;
        private const int DEFAULT_SCENE_MARGIN_PARCELS = 1;

        /// <summary>A live scene image is for inspection; past this side it is downscaled when written.</summary>
        private const int MAX_SCENE_OUTPUT_PIXELS = 4096;
        private const int DEFAULT_CHUNK_SIZE = 32;
        private const float DEFAULT_HOUR = 10f;
        private const float DEFAULT_CAMERA_HEIGHT = 200f;
        private const float MIN_CAMERA_HEIGHT = 20f;
        private const float MAX_CAMERA_HEIGHT = 1000f;
        private const float DEFAULT_LOAD_TIMEOUT_SEC = 180f;
        private const float MIN_LOAD_TIMEOUT_SEC = 10f;
        private const float MAX_LOAD_TIMEOUT_SEC = 3600f;
        private const string DEFAULT_OUTPUT_FOLDER = "map-capture";
        private const int DEFAULT_JPEG_QUALITY = 95;

        // The client's satellite grid: 320x320 parcels from parcel (-152, 152) at its north-west corner, level L split into
        // 2^L x 2^L square tiles with i counting eastward and j southward. Level 3 is the minimap's 8x8 grid of 40-parcel
        // chunks, measured against the chunks the client downloads today.
        private const int GRID_PARCELS = 320;
        private const int MAX_GRID_LEVEL = 6;
        private const int CLIENT_DEFAULT_LEVEL = 3;
        private const int WORLDS_DEFAULT_LEVEL = 4;
        private const int DEFAULT_TILE_PIXELS = 512;
        private const int MIN_TILE_PIXELS = 64;

        // Tiles render a quarter larger than written and are downscaled on write: 16 px per parcel for a 40-parcel tile at 512 px.
        private const int TILE_OVERSAMPLE_NUMERATOR = 5;
        private const int TILE_OVERSAMPLE_DENOMINATOR = 4;
        private const int CLIENT_DEFAULT_LOAD_PARCELS = 20;
        private const int CLIENT_JPEG_QUALITY = 90;
        private const string ALL_WORLDS = "all";
        private static readonly Vector2Int GRID_MIN = new (-152, 152 - GRID_PARCELS + 1);
        private static readonly Vector2Int GRID_MAX = GRID_MIN + (Vector2Int.one * (GRID_PARCELS - 1));

        /// <summary>Inclusive parcel bounds of the region.</summary>
        public readonly Vector2Int Min;
        public readonly Vector2Int Max;

        /// <summary>Blocks tile the region from this corner; the region's minimum unless a fixed grid is captured.</summary>
        public readonly Vector2Int GridOrigin;
        public readonly string OutputDir;

        /// <summary>Folder for Unity's asset bundle cache, or null to leave the default cache alone.</summary>
        public readonly string? CacheDir;

        /// <summary>Parcels per rendered block side; each block becomes one image.</summary>
        public readonly int BlockSize;
        public readonly int PixelsPerParcel;

        /// <summary>Side of the written image; the render is downscaled to it when smaller than the native size.</summary>
        public readonly int OutputPixels;

        /// <summary>JPEG quality of the written images, or null for PNG.</summary>
        public readonly int? JpegQuality;
        public readonly bool ClientMap;
        public readonly bool KeepBloom;

        /// <summary>
        ///     Parcels per loaded chunk side. Either a multiple of <see cref="BlockSize" /> (several images per load) or a
        ///     divisor of it (one image assembled from several loads), so a load and an image never straddle each other.
        /// </summary>
        public readonly int ChunkSize;
        public readonly float Hour;
        public readonly float CameraHeight;
        public readonly float LoadTimeoutSec;

        /// <summary>Parcel of the one scene to run live instead of capturing a region, or null for a region capture.</summary>
        public readonly Vector2Int? SceneParcel;

        /// <summary>Parcels of surroundings rendered around a live scene on every side.</summary>
        public readonly int SceneMarginParcels;

        /// <summary>Side cap of the written live scene image; larger renders are downscaled on write.</summary>
        public readonly int SceneOutputPixels;

        /// <summary>Satellite grid level of the tiles in client-map and worlds modes; a tile is <see cref="BlockSize" /> parcels.</summary>
        public readonly int Level;

        /// <summary>"all" or the path of a world list file in worlds mode, null otherwise.</summary>
        public readonly string? Worlds;

        public bool CapturesWorlds => Worlds != null;

        /// <summary>Worlds come from the worlds content server's index rather than a list file.</summary>
        public bool CapturesAllWorlds => string.Equals(Worlds, ALL_WORLDS, StringComparison.OrdinalIgnoreCase);

        public int RenderPixels => BlockSize * PixelsPerParcel;

        private MapCaptureArgs(Vector2Int min, Vector2Int max, Vector2Int gridOrigin, string outputDir, string? cacheDir, int blockSize, int pixelsPerParcel, int outputPixels, int? jpegQuality,
            bool clientMap, bool keepBloom, int chunkSize, float hour, float cameraHeight, float loadTimeoutSec, Vector2Int? sceneParcel = null, int level = 0,
            string? worlds = null)
        {
            SceneParcel = sceneParcel;
            Level = level;
            Worlds = worlds;
            SceneMarginParcels = DEFAULT_SCENE_MARGIN_PARCELS;
            SceneOutputPixels = MAX_SCENE_OUTPUT_PIXELS;
            KeepBloom = keepBloom;
            Min = min;
            Max = max;
            GridOrigin = gridOrigin;
            OutputDir = outputDir;
            CacheDir = cacheDir;
            BlockSize = blockSize;
            PixelsPerParcel = pixelsPerParcel;
            OutputPixels = outputPixels;
            JpegQuality = jpegQuality;
            ClientMap = clientMap;
            ChunkSize = chunkSize;
            Hour = hour;
            CameraHeight = cameraHeight;
            LoadTimeoutSec = loadTimeoutSec;
        }

        public static bool TryParse(IAppArgs args, out MapCaptureArgs result, out string error)
        {
            result = default(MapCaptureArgs);
            error = string.Empty;

            bool clientMap = args.HasFlag(AppArgsFlags.MapCapture.CLIENT_MAP);
            bool keepBloom = args.HasFlag(AppArgsFlags.MapCapture.KEEP_BLOOM);
            Vector2Int min = Vector2Int.zero;
            Vector2Int max = Vector2Int.zero;
            bool hasRegion = args.TryGetValue(AppArgsFlags.MapCapture.REGION, out string? region) && TryParseRegion(region, out min, out max);
            bool hasScene = args.TryGetValue(AppArgsFlags.MapCapture.SCENE, out string? scene);
            bool hasWorlds = args.TryGetValue(AppArgsFlags.MapCapture.WORLDS, out string? worlds) && !string.IsNullOrWhiteSpace(worlds);
            Vector2Int sceneParcel = Vector2Int.zero;

            if (hasScene && !TryParseParcel(scene, out sceneParcel))
            {
                error = $"--{AppArgsFlags.MapCapture.SCENE} expects a parcel x,y.";
                return false;
            }

            if (!hasRegion && !hasScene && !hasWorlds)
            {
                if (!clientMap)
                {
                    error = $"--{AppArgsFlags.MapCapture.REGION} x0,y0,x1,y1 is required.";
                    return false;
                }

                min = GRID_MIN;
                max = GRID_MAX;
            }

            float hour = Mathf.Clamp(ReadFloat(args, AppArgsFlags.MapCapture.HOUR, DEFAULT_HOUR), 0f, 23.99f);
            float cameraHeight = Mathf.Clamp(ReadFloat(args, AppArgsFlags.MapCapture.CAMERA_HEIGHT, DEFAULT_CAMERA_HEIGHT), MIN_CAMERA_HEIGHT, MAX_CAMERA_HEIGHT);
            float loadTimeoutSec = Mathf.Clamp(ReadFloat(args, AppArgsFlags.MapCapture.LOAD_TIMEOUT_SEC, DEFAULT_LOAD_TIMEOUT_SEC), MIN_LOAD_TIMEOUT_SEC, MAX_LOAD_TIMEOUT_SEC);

            string outputDir = args.TryGetValue(AppArgsFlags.MapCapture.OUTPUT_DIR, out string? dir) && !string.IsNullOrWhiteSpace(dir)
                ? dir
                : Path.Combine(Application.persistentDataPath, DEFAULT_OUTPUT_FOLDER);

            string? cacheDir = args.TryGetValue(AppArgsFlags.MapCapture.CACHE_DIR, out string? cache) && !string.IsNullOrWhiteSpace(cache) ? cache : null;

            int? jpegQuality = args.HasFlag(AppArgsFlags.MapCapture.JPEG)
                ? Mathf.Clamp(ReadInt(args, AppArgsFlags.MapCapture.JPEG, DEFAULT_JPEG_QUALITY), 1, 100)
                : null;

            if (hasScene)
            {
                // One scene, one image, always JPEG like the map tiles it is compared against: the block is the scene's
                // footprint and is sized once the definition is known.
                int scenePixelsPerParcel = Mathf.Max(ReadInt(args, AppArgsFlags.MapCapture.PIXELS_PER_PARCEL, DEFAULT_PIXELS_PER_PARCEL), MIN_PIXELS_PER_PARCEL);

                result = new MapCaptureArgs(sceneParcel, sceneParcel, sceneParcel, outputDir, cacheDir, 1, scenePixelsPerParcel, scenePixelsPerParcel, jpegQuality ?? CLIENT_JPEG_QUALITY,
                    false, keepBloom, 1, hour, cameraHeight, loadTimeoutSec, sceneParcel);

                return true;
            }

            if (hasWorlds)
            {
                // Every world is tiled on the client's grid; which tiles a world needs is known once its parcels are.
                if (!TryParseGrid(args, WORLDS_DEFAULT_LEVEL, out int level, out int tileParcels, out int renderPixelsPerParcel, out int tilePixels, out int loadSize, out error))
                    return false;

                result = new MapCaptureArgs(GRID_MIN, GRID_MAX, GRID_MIN, outputDir, cacheDir, tileParcels, renderPixelsPerParcel, tilePixels, jpegQuality ?? DEFAULT_JPEG_QUALITY,
                    false, keepBloom, loadSize, hour, cameraHeight, loadTimeoutSec, level: level, worlds: worlds!.Trim());

                return true;
            }

            if (clientMap)
            {
                if (!TryParseGrid(args, CLIENT_DEFAULT_LEVEL, out int level, out int tileParcels, out int renderPixelsPerParcel, out int tilePixels, out int loadSize, out error))
                    return false;

                min = Vector2Int.Max(min, GRID_MIN);
                max = Vector2Int.Min(max, GRID_MAX);

                if (min.x > max.x || min.y > max.y)
                {
                    error = "The region lies outside the client's satellite grid.";
                    return false;
                }

                result = new MapCaptureArgs(min, max, GRID_MIN, outputDir, cacheDir, tileParcels, renderPixelsPerParcel, tilePixels, CLIENT_JPEG_QUALITY,
                    true, keepBloom, loadSize, hour, cameraHeight, loadTimeoutSec, level: level);

                return true;
            }

            int blockSize = Mathf.Clamp(ReadInt(args, AppArgsFlags.MapCapture.BLOCK_SIZE, DEFAULT_BLOCK_SIZE), 1, MAX_BLOCK_SIZE);
            int pixelsPerParcel = Mathf.Max(ReadInt(args, AppArgsFlags.MapCapture.PIXELS_PER_PARCEL, DEFAULT_PIXELS_PER_PARCEL), MIN_PIXELS_PER_PARCEL);

            if (blockSize * pixelsPerParcel > MAX_RENDER_PIXELS)
            {
                error = $"Block of {blockSize} parcels at {pixelsPerParcel} px/parcel exceeds the {MAX_RENDER_PIXELS} px render cap.";
                return false;
            }

            int chunkSize = Mathf.Max(ReadInt(args, AppArgsFlags.MapCapture.CHUNK_SIZE, DEFAULT_CHUNK_SIZE), 1);

            if (chunkSize >= blockSize)
                chunkSize = (chunkSize + blockSize - 1) / blockSize * blockSize;
            else if (blockSize % chunkSize != 0)
                chunkSize = blockSize;

            result = new MapCaptureArgs(min, max, min, outputDir, cacheDir, blockSize, pixelsPerParcel, blockSize * pixelsPerParcel, jpegQuality,
                false, keepBloom, chunkSize, hour, cameraHeight, loadTimeoutSec);

            return true;
        }

        /// <summary>Column and row on the satellite grid of the tile whose minimum parcel is <paramref name="tileMin" />.</summary>
        public Vector2Int TileIndex(Vector2Int tileMin) =>
            new ((tileMin.x - GRID_MIN.x) / BlockSize, (GRID_MAX.y - (tileMin.y + BlockSize - 1)) / BlockSize);

        /// <summary>Minimum parcel of the grid tile <paramref name="tile" />.</summary>
        public Vector2Int TileMin(Vector2Int tile) =>
            new (GRID_MIN.x + (tile.x * BlockSize), GRID_MAX.y - ((tile.y + 1) * BlockSize) + 1);

        /// <summary>The grid tile holding <paramref name="parcel" />, or null when the parcel lies outside the grid.</summary>
        public Vector2Int? TileOf(Vector2Int parcel)
        {
            if (parcel.x < GRID_MIN.x || parcel.x > GRID_MAX.x || parcel.y < GRID_MIN.y || parcel.y > GRID_MAX.y)
                return null;

            return new Vector2Int((parcel.x - GRID_MIN.x) / BlockSize, (GRID_MAX.y - parcel.y) / BlockSize);
        }

        /// <summary>The client's tile name "i,j.jpg" of the block at <paramref name="blockMin" />; only meaningful on the grid.</summary>
        public string TileName(Vector2Int blockMin)
        {
            Vector2Int tile = TileIndex(blockMin);
            return $"{tile.x},{tile.y}.jpg";
        }

        /// <summary>
        ///     Tile size, render scale and load size of a grid level. A load is a whole fraction of a tile, so a tile is
        ///     assembled from loads and no load straddles two tiles.
        /// </summary>
        private static bool TryParseGrid(IAppArgs args, int defaultLevel, out int level, out int tileParcels, out int renderPixelsPerParcel, out int tilePixels,
            out int loadSize, out string error)
        {
            error = string.Empty;
            tileParcels = renderPixelsPerParcel = tilePixels = loadSize = 0;
            level = ReadInt(args, AppArgsFlags.MapCapture.LEVEL, defaultLevel);

            if (level < 0 || level > MAX_GRID_LEVEL)
            {
                error = $"--{AppArgsFlags.MapCapture.LEVEL} must be between 0 and {MAX_GRID_LEVEL}.";
                return false;
            }

            tileParcels = GRID_PARCELS >> level;
            tilePixels = Mathf.Clamp(ReadInt(args, AppArgsFlags.MapCapture.TILE_PIXELS, DEFAULT_TILE_PIXELS), MIN_TILE_PIXELS, MAX_RENDER_PIXELS);

            // Rounded up, then capped so a whole tile still fits one render target.
            int oversampledDenominator = TILE_OVERSAMPLE_DENOMINATOR * tileParcels;
            int oversampled = ((tilePixels * TILE_OVERSAMPLE_NUMERATOR) + oversampledDenominator - 1) / oversampledDenominator;
            renderPixelsPerParcel = Mathf.Min(oversampled, MAX_RENDER_PIXELS / tileParcels);

            if (renderPixelsPerParcel * tileParcels < tilePixels)
            {
                error = $"A {tilePixels} px tile exceeds the {MAX_RENDER_PIXELS} px render cap at level {level}.";
                return false;
            }

            int defaultLoad = Mathf.Min(CLIENT_DEFAULT_LOAD_PARCELS, tileParcels);
            loadSize = ReadInt(args, AppArgsFlags.MapCapture.CHUNK_SIZE, defaultLoad);

            if (loadSize <= 0 || loadSize > tileParcels || tileParcels % loadSize != 0)
                loadSize = defaultLoad;

            return true;
        }

        private static bool TryParseRegion(string? value, out Vector2Int min, out Vector2Int max)
        {
            min = max = Vector2Int.zero;

            if (string.IsNullOrEmpty(value)) return false;

            string[] parts = value.Split(',');

            if (parts.Length != 4) return false;

            var numbers = new int[4];

            for (var i = 0; i < 4; i++)
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i]))
                    return false;

            min = new Vector2Int(Mathf.Min(numbers[0], numbers[2]), Mathf.Min(numbers[1], numbers[3]));
            max = new Vector2Int(Mathf.Max(numbers[0], numbers[2]), Mathf.Max(numbers[1], numbers[3]));
            return true;
        }

        private static bool TryParseParcel(string? value, out Vector2Int parcel)
        {
            parcel = Vector2Int.zero;

            if (string.IsNullOrEmpty(value)) return false;

            string[] parts = value.Split(',');

            if (parts.Length != 2
                || !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                return false;

            parcel = new Vector2Int(x, y);
            return true;
        }

        private static int ReadInt(IAppArgs args, string key, int defaultValue) =>
            args.TryGetValue(key, out string? value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : defaultValue;

        private static float ReadFloat(IAppArgs args, string key, float defaultValue) =>
            args.TryGetValue(key, out string? value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : defaultValue;
    }
}
