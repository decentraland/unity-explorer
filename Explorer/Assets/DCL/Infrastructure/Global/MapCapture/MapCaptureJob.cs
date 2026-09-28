using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SkyBox;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Global.MapCapture
{
    /// <summary>
    ///     Walks the region chunk by chunk: loads a chunk's scenes, renders every block inside it to a PNG, unloads
    ///     the chunk and moves on. Writes a manifest listing each block and the parcels that never finished loading.
    /// </summary>
    public class MapCaptureJob
    {
        private const int POLL_INTERVAL_MS = 250;
        private const int SKYBOX_POLL_INTERVAL_MS = 50;
        private const int READY_SETTLE_FRAMES = 2;
        private const int UNLOAD_SETTLE_FRAMES = 3;
        private const float SKYBOX_SETTLE_EPSILON = 0.001f;
        private const float SKYBOX_TIMEOUT_SEC = 15f;
        private const string MANIFEST_FILE = "manifest.json";

        private readonly MapCaptureRuntime runtime;
        private readonly MapCaptureArgs args;

        public readonly struct Summary
        {
            public readonly int Blocks;
            public readonly int Complete;

            public bool AllComplete => Complete == Blocks;

            public Summary(int blocks, int complete)
            {
                Blocks = blocks;
                Complete = complete;
            }

            public override string ToString() =>
                $"{Complete}/{Blocks} blocks rendered with every scene loaded";
        }

        public MapCaptureJob(MapCaptureRuntime runtime, MapCaptureArgs args)
        {
            this.runtime = runtime;
            this.args = args;
        }

        public async UniTask<Summary> RunAsync(CancellationToken ct)
        {
            Directory.CreateDirectory(args.OutputDir);
            await FixSkyboxAsync(ct);

            // Blocks tile the grid from its origin; the region is widened to whole blocks so every image is a full
            // block. Chunks are block multiples, so a block never straddles two.
            var blocksMin = new Vector2Int(
                args.GridOrigin.x + (FloorDiv(args.Min.x - args.GridOrigin.x, args.BlockSize) * args.BlockSize),
                args.GridOrigin.y + (FloorDiv(args.Min.y - args.GridOrigin.y, args.BlockSize) * args.BlockSize));

            var blocksMax = new Vector2Int(
                args.GridOrigin.x + (CeilDiv(args.Max.x - args.GridOrigin.x + 1, args.BlockSize) * args.BlockSize) - 1,
                args.GridOrigin.y + (CeilDiv(args.Max.y - args.GridOrigin.y + 1, args.BlockSize) * args.BlockSize) - 1);

            var blocks = new JArray();
            var total = 0;
            var complete = 0;

            for (int cy = blocksMin.y; cy <= blocksMax.y; cy += args.ChunkSize)
            for (int cx = blocksMin.x; cx <= blocksMax.x; cx += args.ChunkSize)
            {
                var chunkMin = new Vector2Int(cx, cy);
                var chunkMax = new Vector2Int(Mathf.Min(cx + args.ChunkSize - 1, blocksMax.x), Mathf.Min(cy + args.ChunkSize - 1, blocksMax.y));

                List<Vector2Int> parcels = ParcelsIn(chunkMin, chunkMax);
                runtime.Feeder.Request(parcels);
                await WaitForParcelsAsync(parcels, ct);

                for (int by = chunkMin.y; by <= chunkMax.y; by += args.BlockSize)
                for (int bx = chunkMin.x; bx <= chunkMax.x; bx += args.BlockSize)
                {
                    var blockMin = new Vector2Int(bx, by);
                    (JArray pending, JArray failed) = BlockStatus(blockMin);

                    byte[] image = await runtime.Camera.RenderBlockAsync(blockMin, args.BlockSize, args.RenderPixels, args.OutputPixels, args.Jpeg, args.CameraHeight, ct);
                    string file = args.ClientMap ? args.ClientChunkName(blockMin) : $"{bx}_{by}.png";
                    File.WriteAllBytes(Path.Combine(args.OutputDir, file), image);

                    total++;

                    if (pending.Count == 0 && failed.Count == 0)
                        complete++;

                    blocks.Add(new JObject
                    {
                        ["x"] = bx,
                        ["y"] = by,
                        ["file"] = file,
                        ["pendingParcels"] = pending,
                        ["failedParcels"] = failed,
                    });

                    ReportHub.Log(ReportCategory.ENGINE, $"[MapCapture] block ({bx},{by}) written; pending {pending.Count}, failed {failed.Count}");
                }

                runtime.Feeder.UnloadAll(runtime.World);
                await UniTask.DelayFrame(UNLOAD_SETTLE_FRAMES, cancellationToken: ct);
            }

            var manifest = new JObject
            {
                ["region"] = new JObject { ["minX"] = args.Min.x, ["minY"] = args.Min.y, ["maxX"] = args.Max.x, ["maxY"] = args.Max.y },
                ["clientMap"] = args.ClientMap,
                ["gridOrigin"] = new JObject { ["x"] = args.GridOrigin.x, ["y"] = args.GridOrigin.y },
                ["blockSize"] = args.BlockSize,
                ["pixelsPerParcel"] = args.PixelsPerParcel,
                ["outputPixels"] = args.OutputPixels,
                ["hour"] = args.Hour,
                ["cameraHeight"] = args.CameraHeight,
                ["blocks"] = blocks,
            };

            File.WriteAllText(Path.Combine(args.OutputDir, MANIFEST_FILE), manifest.ToString(Formatting.Indented));
            return new Summary(total, complete);
        }

        private async UniTask FixSkyboxAsync(CancellationToken ct)
        {
            SkyboxSettingsAsset skybox = runtime.SkyboxSettings;
            float normalized = Mathf.Clamp01(args.Hour * 60f / SkyboxSettingsAsset.TOTAL_MINUTES_IN_DAY);

            skybox.IsUIControlled = true;
            skybox.UIOverrideTimeOfDayNormalized = normalized;

            // The skybox interpolates towards the target rather than snapping; re-assert until the live value lands.
            float deadline = UnityEngine.Time.realtimeSinceStartup + SKYBOX_TIMEOUT_SEC;

            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                skybox.TargetTimeOfDayNormalized = normalized;
                skybox.TimeOfDayNormalized = normalized;

                if (Mathf.Abs(skybox.TimeOfDayNormalized - normalized) <= SKYBOX_SETTLE_EPSILON)
                    return;

                await UniTask.Delay(SKYBOX_POLL_INTERVAL_MS, cancellationToken: ct);
            }

            ReportHub.LogWarning(ReportCategory.ENGINE, "[MapCapture] The skybox did not settle at the requested hour; capturing anyway");
        }

        private async UniTask WaitForParcelsAsync(List<Vector2Int> parcels, CancellationToken ct)
        {
            float deadline = UnityEngine.Time.realtimeSinceStartup + args.LoadTimeoutSec;

            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                if (!runtime.Feeder.HasRequestsInFlight && AllReady(parcels))
                {
                    await UniTask.DelayFrame(READY_SETTLE_FRAMES, cancellationToken: ct);
                    return;
                }

                await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
            }

            ReportHub.LogWarning(ReportCategory.ENGINE, $"[MapCapture] Chunk at ({parcels[0].x},{parcels[0].y}) did not finish loading within {args.LoadTimeoutSec:0}s; rendering what is there");
        }

        private bool AllReady(List<Vector2Int> parcels)
        {
            foreach (Vector2Int parcel in parcels)
                if (!runtime.Feeder.IsParcelReady(runtime.World, parcel, out _))
                    return false;

            return true;
        }

        private (JArray pending, JArray failed) BlockStatus(Vector2Int blockMin)
        {
            var pending = new JArray();
            var failed = new JArray();

            for (int dy = 0; dy < args.BlockSize; dy++)
            for (int dx = 0; dx < args.BlockSize; dx++)
            {
                var parcel = new Vector2Int(blockMin.x + dx, blockMin.y + dy);

                if (!runtime.Feeder.IsParcelReady(runtime.World, parcel, out bool parcelFailed))
                    pending.Add(ParcelJson(parcel));
                else if (parcelFailed)
                    failed.Add(ParcelJson(parcel));
            }

            return (pending, failed);
        }

        private static List<Vector2Int> ParcelsIn(Vector2Int min, Vector2Int max)
        {
            var parcels = new List<Vector2Int>((max.x - min.x + 1) * (max.y - min.y + 1));

            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
                parcels.Add(new Vector2Int(x, y));

            return parcels;
        }

        private static JObject ParcelJson(Vector2Int parcel) =>
            new () { ["x"] = parcel.x, ["y"] = parcel.y };

        private static int CeilDiv(int value, int divisor) =>
            FloorDiv(value + divisor - 1, divisor);

        private static int FloorDiv(int value, int divisor) =>
            (value - (((value % divisor) + divisor) % divisor)) / divisor;
    }
}
