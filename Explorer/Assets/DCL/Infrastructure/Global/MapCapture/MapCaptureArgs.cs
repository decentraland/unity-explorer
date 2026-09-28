using Global.AppArgs;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Global.MapCapture
{
    /// <summary>Launch-argument configuration of one capture run (see <see cref="AppArgsFlags.MapCapture" />).</summary>
    public readonly struct MapCaptureArgs
    {
        private const int DEFAULT_BLOCK_SIZE = 8;
        private const int MAX_BLOCK_SIZE = 40;
        private const int DEFAULT_PIXELS_PER_PARCEL = 256;
        private const int MIN_PIXELS_PER_PARCEL = 16;
        private const int MAX_PIXELS = 8192;
        private const int DEFAULT_CHUNK_SIZE = 32;
        private const float DEFAULT_HOUR = 10f;
        private const float DEFAULT_CAMERA_HEIGHT = 200f;
        private const float MIN_CAMERA_HEIGHT = 20f;
        private const float MAX_CAMERA_HEIGHT = 1000f;
        private const float DEFAULT_LOAD_TIMEOUT_SEC = 180f;
        private const float MIN_LOAD_TIMEOUT_SEC = 10f;
        private const float MAX_LOAD_TIMEOUT_SEC = 3600f;
        private const string DEFAULT_OUTPUT_FOLDER = "map-capture";

        // The minimap's satellite layer: an 8x8 grid of 40-parcel chunks at 512 px, whose first chunk starts at parcel
        // (-152, 113) and whose rows count southward. Measured against the chunks the client downloads today.
        private const int CLIENT_CHUNK_PARCELS = 40;
        private const int CLIENT_CHUNK_PIXELS = 512;
        private const int CLIENT_RENDER_PIXELS_PER_PARCEL = 16;
        private const int CLIENT_GRID_CHUNKS = 8;
        private static readonly Vector2Int CLIENT_GRID_MIN = new (-152, 113 - (CLIENT_CHUNK_PARCELS * (CLIENT_GRID_CHUNKS - 1)));
        private static readonly Vector2Int CLIENT_GRID_MAX = CLIENT_GRID_MIN + (Vector2Int.one * ((CLIENT_CHUNK_PARCELS * CLIENT_GRID_CHUNKS) - 1));

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
        public readonly bool Jpeg;
        public readonly bool ClientMap;

        /// <summary>Parcels per loaded chunk side, always a multiple of <see cref="BlockSize" />.</summary>
        public readonly int ChunkSize;
        public readonly float Hour;
        public readonly float CameraHeight;
        public readonly float LoadTimeoutSec;

        public int RenderPixels => BlockSize * PixelsPerParcel;

        private MapCaptureArgs(Vector2Int min, Vector2Int max, Vector2Int gridOrigin, string outputDir, string? cacheDir, int blockSize, int pixelsPerParcel, int outputPixels, bool jpeg,
            bool clientMap, int chunkSize, float hour, float cameraHeight, float loadTimeoutSec)
        {
            Min = min;
            Max = max;
            GridOrigin = gridOrigin;
            OutputDir = outputDir;
            CacheDir = cacheDir;
            BlockSize = blockSize;
            PixelsPerParcel = pixelsPerParcel;
            OutputPixels = outputPixels;
            Jpeg = jpeg;
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
            Vector2Int min = Vector2Int.zero;
            Vector2Int max = Vector2Int.zero;
            bool hasRegion = args.TryGetValue(AppArgsFlags.MapCapture.REGION, out string? region) && TryParseRegion(region, out min, out max);

            if (!hasRegion)
            {
                if (!clientMap)
                {
                    error = $"--{AppArgsFlags.MapCapture.REGION} x0,y0,x1,y1 is required.";
                    return false;
                }

                min = CLIENT_GRID_MIN;
                max = CLIENT_GRID_MAX;
            }

            float hour = Mathf.Clamp(ReadFloat(args, AppArgsFlags.MapCapture.HOUR, DEFAULT_HOUR), 0f, 23.99f);
            float cameraHeight = Mathf.Clamp(ReadFloat(args, AppArgsFlags.MapCapture.CAMERA_HEIGHT, DEFAULT_CAMERA_HEIGHT), MIN_CAMERA_HEIGHT, MAX_CAMERA_HEIGHT);
            float loadTimeoutSec = Mathf.Clamp(ReadFloat(args, AppArgsFlags.MapCapture.LOAD_TIMEOUT_SEC, DEFAULT_LOAD_TIMEOUT_SEC), MIN_LOAD_TIMEOUT_SEC, MAX_LOAD_TIMEOUT_SEC);

            string outputDir = args.TryGetValue(AppArgsFlags.MapCapture.OUTPUT_DIR, out string? dir) && !string.IsNullOrWhiteSpace(dir)
                ? dir
                : Path.Combine(Application.persistentDataPath, DEFAULT_OUTPUT_FOLDER);

            string? cacheDir = args.TryGetValue(AppArgsFlags.MapCapture.CACHE_DIR, out string? cache) && !string.IsNullOrWhiteSpace(cache) ? cache : null;

            if (clientMap)
            {
                min = Vector2Int.Max(min, CLIENT_GRID_MIN);
                max = Vector2Int.Min(max, CLIENT_GRID_MAX);

                if (min.x > max.x || min.y > max.y)
                {
                    error = "The region lies outside the client's satellite grid.";
                    return false;
                }

                result = new MapCaptureArgs(min, max, CLIENT_GRID_MIN, outputDir, cacheDir, CLIENT_CHUNK_PARCELS, CLIENT_RENDER_PIXELS_PER_PARCEL, CLIENT_CHUNK_PIXELS, true,
                    true, CLIENT_CHUNK_PARCELS, hour, cameraHeight, loadTimeoutSec);

                return true;
            }

            int blockSize = Mathf.Clamp(ReadInt(args, AppArgsFlags.MapCapture.BLOCK_SIZE, DEFAULT_BLOCK_SIZE), 1, MAX_BLOCK_SIZE);
            int pixelsPerParcel = Mathf.Max(ReadInt(args, AppArgsFlags.MapCapture.PIXELS_PER_PARCEL, DEFAULT_PIXELS_PER_PARCEL), MIN_PIXELS_PER_PARCEL);

            if (blockSize * pixelsPerParcel > MAX_PIXELS)
            {
                error = $"Block of {blockSize} parcels at {pixelsPerParcel} px/parcel exceeds the {MAX_PIXELS} px render cap.";
                return false;
            }

            int chunkSize = Mathf.Max(ReadInt(args, AppArgsFlags.MapCapture.CHUNK_SIZE, DEFAULT_CHUNK_SIZE), blockSize);
            chunkSize = (chunkSize + blockSize - 1) / blockSize * blockSize;

            result = new MapCaptureArgs(min, max, min, outputDir, cacheDir, blockSize, pixelsPerParcel, blockSize * pixelsPerParcel, false,
                false, chunkSize, hour, cameraHeight, loadTimeoutSec);

            return true;
        }

        /// <summary>The client's chunk name for a block at <paramref name="blockMin" />; only meaningful in client-map mode.</summary>
        public string ClientChunkName(Vector2Int blockMin)
        {
            int i = (blockMin.x - CLIENT_GRID_MIN.x) / CLIENT_CHUNK_PARCELS;
            int j = (CLIENT_GRID_MAX.y - blockMin.y) / CLIENT_CHUNK_PARCELS;
            return $"{i},{j}.jpg";
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

        private static int ReadInt(IAppArgs args, string key, int defaultValue) =>
            args.TryGetValue(key, out string? value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : defaultValue;

        private static float ReadFloat(IAppArgs args, string key, float defaultValue) =>
            args.TryGetValue(key, out string? value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : defaultValue;
    }
}
