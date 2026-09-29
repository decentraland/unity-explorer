using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SkyBox;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
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
        private const int TIMEOUT_REPORT_LIMIT = 40;

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
            // block. A load is either several whole blocks or a whole fraction of one, never straddling an image.
            var blocksMin = new Vector2Int(
                args.GridOrigin.x + (FloorDiv(args.Min.x - args.GridOrigin.x, args.BlockSize) * args.BlockSize),
                args.GridOrigin.y + (FloorDiv(args.Min.y - args.GridOrigin.y, args.BlockSize) * args.BlockSize));

            var blocksMax = new Vector2Int(
                args.GridOrigin.x + (CeilDiv(args.Max.x - args.GridOrigin.x + 1, args.BlockSize) * args.BlockSize) - 1,
                args.GridOrigin.y + (CeilDiv(args.Max.y - args.GridOrigin.y + 1, args.BlockSize) * args.BlockSize) - 1);

            var blocks = new JArray();
            var total = 0;
            var complete = 0;

            if (args.ChunkSize >= args.BlockSize)
                await RenderBlocksPerLoadAsync(blocksMin, blocksMax, blocks, ct, () => total++, () => complete++);
            else
                await RenderLoadsPerBlockAsync(blocksMin, blocksMax, blocks, ct, () => total++, () => complete++);

            var manifest = new JObject
            {
                ["region"] = new JObject { ["minX"] = args.Min.x, ["minY"] = args.Min.y, ["maxX"] = args.Max.x, ["maxY"] = args.Max.y },
                ["clientMap"] = args.ClientMap,
                ["gridOrigin"] = new JObject { ["x"] = args.GridOrigin.x, ["y"] = args.GridOrigin.y },
                ["blockSize"] = args.BlockSize,
                ["chunkSize"] = args.ChunkSize,
                ["pixelsPerParcel"] = args.PixelsPerParcel,
                ["outputPixels"] = args.OutputPixels,
                ["hour"] = args.Hour,
                ["cameraHeight"] = args.CameraHeight,
                ["blocks"] = blocks,
            };

            File.WriteAllText(Path.Combine(args.OutputDir, MANIFEST_FILE), manifest.ToString(Formatting.Indented));
            return new Summary(total, complete);
        }

        /// <summary>A load covers several blocks: load once, render each block, unload.</summary>
        private async UniTask RenderBlocksPerLoadAsync(Vector2Int blocksMin, Vector2Int blocksMax, JArray blocks, CancellationToken ct, Action onBlock, Action onComplete)
        {
            for (int cy = blocksMin.y; cy <= blocksMax.y; cy += args.ChunkSize)
            for (int cx = blocksMin.x; cx <= blocksMax.x; cx += args.ChunkSize)
            {
                var chunkMin = new Vector2Int(cx, cy);
                var chunkMax = new Vector2Int(Mathf.Min(cx + args.ChunkSize - 1, blocksMax.x), Mathf.Min(cy + args.ChunkSize - 1, blocksMax.y));

                await LoadAsync(chunkMin, chunkMax, ct);

                for (int by = chunkMin.y; by <= chunkMax.y; by += args.BlockSize)
                for (int bx = chunkMin.x; bx <= chunkMax.x; bx += args.BlockSize)
                {
                    var blockMin = new Vector2Int(bx, by);
                    var pending = new JArray();
                    var failed = new JArray();
                    CollectStatus(blockMin, args.BlockSize, pending, failed);

                    byte[] image = await runtime.Camera.RenderBlockAsync(blockMin, args.BlockSize, args.RenderPixels, args.OutputPixels, args.Jpeg, args.CameraHeight, ct);
                    WriteBlock(blockMin, image, pending, failed, blocks, onBlock, onComplete);
                }

                await UnloadAsync(ct);
            }
        }

        /// <summary>A block needs several loads: load a part, render it into the image, unload, repeat, then write.</summary>
        private async UniTask RenderLoadsPerBlockAsync(Vector2Int blocksMin, Vector2Int blocksMax, JArray blocks, CancellationToken ct, Action onBlock, Action onComplete)
        {
            int partPixels = args.RenderPixels / (args.BlockSize / args.ChunkSize);

            for (int by = blocksMin.y; by <= blocksMax.y; by += args.BlockSize)
            for (int bx = blocksMin.x; bx <= blocksMax.x; bx += args.BlockSize)
            {
                var blockMin = new Vector2Int(bx, by);
                var pending = new JArray();
                var failed = new JArray();
                MapCaptureCamera.BlockImage image = runtime.Camera.BeginBlock(args.RenderPixels);

                for (int py = 0; py < args.BlockSize; py += args.ChunkSize)
                for (int px = 0; px < args.BlockSize; px += args.ChunkSize)
                {
                    var partMin = new Vector2Int(bx + px, by + py);
                    await LoadAsync(partMin, partMin + (Vector2Int.one * (args.ChunkSize - 1)), ct);
                    CollectStatus(partMin, args.ChunkSize, pending, failed);

                    var pixelOffset = new Vector2Int(px / args.ChunkSize * partPixels, py / args.ChunkSize * partPixels);
                    await runtime.Camera.RenderIntoAsync(image, partMin, args.ChunkSize, pixelOffset, partPixels, args.CameraHeight, ct);
                    await UnloadAsync(ct);
                }

                byte[] bytes = await runtime.Camera.EndBlockAsync(image, args.OutputPixels, args.Jpeg, ct);
                WriteBlock(blockMin, bytes, pending, failed, blocks, onBlock, onComplete);
            }
        }

        private async UniTask LoadAsync(Vector2Int min, Vector2Int max, CancellationToken ct)
        {
            List<Vector2Int> parcels = ParcelsIn(min, max);
            float started = UnityEngine.Time.realtimeSinceStartup;
            Debug.Log($"[JUANI] Load ({min.x},{min.y})-({max.x},{max.y}) started");
            runtime.Feeder.Request(parcels);
            await WaitForParcelsAsync(parcels, ct);
            Debug.Log($"[JUANI] Load ({min.x},{min.y})-({max.x},{max.y}) finished in {UnityEngine.Time.realtimeSinceStartup - started:0.0}s");
        }

        private async UniTask UnloadAsync(CancellationToken ct)
        {
            runtime.Feeder.UnloadAll(runtime.World);
            await UniTask.DelayFrame(UNLOAD_SETTLE_FRAMES, cancellationToken: ct);
        }

        private void WriteBlock(Vector2Int blockMin, byte[] image, JArray pending, JArray failed, JArray blocks, Action onBlock, Action onComplete)
        {
            string file = args.ClientMap ? args.ClientChunkName(blockMin) : $"{blockMin.x}_{blockMin.y}.png";
            File.WriteAllBytes(Path.Combine(args.OutputDir, file), image);

            onBlock();

            if (pending.Count == 0 && failed.Count == 0)
                onComplete();

            blocks.Add(new JObject
            {
                ["x"] = blockMin.x,
                ["y"] = blockMin.y,
                ["file"] = file,
                ["pendingParcels"] = pending,
                ["failedParcels"] = failed,
            });

            ReportHub.Log(ReportCategory.ENGINE, $"[MapCapture] block ({blockMin.x},{blockMin.y}) written to {file}; pending {pending.Count}, failed {failed.Count}");
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

            var pending = new List<string>();

            foreach (Vector2Int parcel in parcels)
                if (!runtime.Feeder.IsParcelReady(runtime.World, parcel, out _))
                    pending.Add($"({parcel.x},{parcel.y}) {runtime.Feeder.DescribeParcel(runtime.World, parcel)}");

            Debug.LogWarning($"[JUANI] Load at ({parcels[0].x},{parcels[0].y}) TIMED OUT after {args.LoadTimeoutSec:0}s with {pending.Count} parcels pending:\n{string.Join("\n", pending.GetRange(0, Mathf.Min(pending.Count, TIMEOUT_REPORT_LIMIT)))}");
        }

        private bool AllReady(List<Vector2Int> parcels)
        {
            foreach (Vector2Int parcel in parcels)
                if (!runtime.Feeder.IsParcelReady(runtime.World, parcel, out _))
                    return false;

            return true;
        }

        private void CollectStatus(Vector2Int min, int size, JArray pending, JArray failed)
        {
            for (int dy = 0; dy < size; dy++)
            for (int dx = 0; dx < size; dx++)
            {
                var parcel = new Vector2Int(min.x + dx, min.y + dy);

                if (!runtime.Feeder.IsParcelReady(runtime.World, parcel, out bool parcelFailed))
                    pending.Add(ParcelJson(parcel));
                else if (parcelFailed)
                    failed.Add(ParcelJson(parcel));
            }
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
