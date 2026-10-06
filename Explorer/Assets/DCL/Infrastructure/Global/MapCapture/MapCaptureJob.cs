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
    ///     With a scene parcel instead of a region, runs that one scene live and renders it alone.
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

            if (args.SceneParcel.HasValue && runtime.LiveScenes != null)
                return await RunLiveSceneAsync(runtime.LiveScenes, args.SceneParcel.Value, ct);

            if (args.CapturesWorlds)
                return await new MapCaptureWorldsJob(runtime, args, this).RunAsync(ct);

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

        /// <summary>
        ///     One scene run live: its footprint plus a margin, centred, is the single image. Complete when the scene
        ///     reported its models loaded before the timeout and did not fail.
        /// </summary>
        private async UniTask<Summary> RunLiveSceneAsync(MapCaptureLiveSceneLoader liveScenes, Vector2Int parcel, CancellationToken ct)
        {
            float started = UnityEngine.Time.realtimeSinceStartup;
            MapCaptureLiveScene? scene = await liveScenes.LoadAsync(parcel, ct);

            if (scene == null)
            {
                ReportHub.LogProductionInfo($"[MapCapture] No scene occupies ({parcel.x},{parcel.y})");
                return new Summary(1, 0);
            }

            string id = scene.Definition.Definition.id;
            bool ready = await scene.WaitUntilReadyAsync(args.LoadTimeoutSec, ct);
            ReportHub.LogProductionInfo($"[MapCapture] Scene {id} {(ready ? "ready" : scene.Failed ? "FAILED" : "TIMED OUT")} after {UnityEngine.Time.realtimeSinceStartup - started:0.0}s");
            await UniTask.DelayFrame(READY_SETTLE_FRAMES, cancellationToken: ct);

            Footprint(scene.Definition.Parcels, args.SceneMarginParcels, out Vector2Int blockMin, out int blockSize);
            int pixelsPerParcel = Mathf.Min(args.PixelsPerParcel, MapCaptureArgs.MAX_RENDER_PIXELS / blockSize);
            int renderPixels = blockSize * pixelsPerParcel;
            int outputPixels = Mathf.Min(renderPixels, args.SceneOutputPixels);

            byte[] image = await runtime.Camera.RenderBlockAsync(blockMin, blockSize, renderPixels, outputPixels, args.JpegQuality, args.CameraHeight, ct);
            string file = $"scene_{parcel.x}_{parcel.y}.{(args.JpegQuality.HasValue ? "jpg" : "png")}";
            File.WriteAllBytes(Path.Combine(args.OutputDir, file), image);

            var parcels = new JArray();

            foreach (Vector2Int sceneParcel in scene.Definition.Parcels)
                parcels.Add(ParcelJson(sceneParcel));

            var manifest = new JObject
            {
                ["scene"] = new JObject { ["id"] = id, ["parcels"] = parcels },
                ["file"] = file,
                ["ready"] = ready,
                ["failed"] = scene.Failed,
                ["block"] = new JObject { ["x"] = blockMin.x, ["y"] = blockMin.y, ["size"] = blockSize },
                ["pixelsPerParcel"] = pixelsPerParcel,
                ["outputPixels"] = outputPixels,
                ["hour"] = args.Hour,
                ["cameraHeight"] = args.CameraHeight,
            };

            File.WriteAllText(Path.Combine(args.OutputDir, MANIFEST_FILE), manifest.ToString(Formatting.Indented));
            ReportHub.LogProductionInfo($"[MapCapture] Scene {id} written to {file}");

            await scene.DisposeAsync();
            await UnloadAsync(ct);

            return new Summary(1, ready ? 1 : 0);
        }

        /// <summary>The smallest square of parcels holding every parcel plus the margin, centred on the footprint.</summary>
        private static void Footprint(IReadOnlyList<Vector2Int> parcels, int margin, out Vector2Int min, out int size)
        {
            Vector2Int footprintMin = parcels[0];
            Vector2Int footprintMax = parcels[0];

            foreach (Vector2Int parcel in parcels)
            {
                footprintMin = Vector2Int.Min(footprintMin, parcel);
                footprintMax = Vector2Int.Max(footprintMax, parcel);
            }

            Vector2Int extent = footprintMax - footprintMin + Vector2Int.one;
            size = Mathf.Max(extent.x, extent.y) + (margin * 2);
            min = footprintMin - new Vector2Int((size - extent.x) / 2, (size - extent.y) / 2);
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

                    byte[] image = await runtime.Camera.RenderBlockAsync(blockMin, args.BlockSize, args.RenderPixels, args.OutputPixels, args.JpegQuality, args.CameraHeight, ct);
                    WriteBlock(blockMin, image, pending, failed, blocks, onBlock, onComplete);
                }

                await UnloadAsync(ct);
            }
        }

        /// <summary>A block needs several loads: load a part, render it into the image, unload, repeat, then write.</summary>
        private async UniTask RenderLoadsPerBlockAsync(Vector2Int blocksMin, Vector2Int blocksMax, JArray blocks, CancellationToken ct, Action onBlock, Action onComplete)
        {
            for (int by = blocksMin.y; by <= blocksMax.y; by += args.BlockSize)
            for (int bx = blocksMin.x; bx <= blocksMax.x; bx += args.BlockSize)
            {
                var blockMin = new Vector2Int(bx, by);
                var pending = new JArray();
                var failed = new JArray();
                byte[] bytes = await RenderBlockInPartsAsync(blockMin, pending, failed, ct);
                WriteBlock(blockMin, bytes, pending, failed, blocks, onBlock, onComplete);
            }
        }

        /// <summary>
        ///     One block assembled from loads of <see cref="MapCaptureArgs.ChunkSize" /> parcels, each rendered into its
        ///     part of the image and unloaded before the next. Returns the encoded image; parcels that never finished
        ///     loading or failed are added to <paramref name="pending" /> and <paramref name="failed" />.
        /// </summary>
        internal async UniTask<byte[]> RenderBlockInPartsAsync(Vector2Int blockMin, JArray pending, JArray failed, CancellationToken ct)
        {
            int partPixels = args.RenderPixels / (args.BlockSize / args.ChunkSize);
            MapCaptureCamera.BlockImage image = runtime.Camera.BeginBlock(args.RenderPixels);

            try
            {
                for (var py = 0; py < args.BlockSize; py += args.ChunkSize)
                for (var px = 0; px < args.BlockSize; px += args.ChunkSize)
                {
                    var partMin = new Vector2Int(blockMin.x + px, blockMin.y + py);
                    await LoadAsync(partMin, partMin + (Vector2Int.one * (args.ChunkSize - 1)), ct);
                    CollectStatus(partMin, args.ChunkSize, pending, failed);

                    var pixelOffset = new Vector2Int(px / args.ChunkSize * partPixels, py / args.ChunkSize * partPixels);
                    await runtime.Camera.RenderIntoAsync(image, partMin, args.ChunkSize, pixelOffset, partPixels, args.CameraHeight, ct);
                    await UnloadAsync(ct);
                }
            }
            catch
            {
                image.Dispose();
                throw;
            }

            return await runtime.Camera.EndBlockAsync(image, args.OutputPixels, args.JpegQuality, ct);
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

        internal async UniTask UnloadAsync(CancellationToken ct)
        {
            bool hadScenes = runtime.Feeder.HasSceneEntities;
            runtime.Feeder.UnloadAll(runtime.World);

            // A load of nothing but empty parcels (most of a world's tiles) left nothing to release.
            if (!hadScenes) return;

            await UniTask.DelayFrame(UNLOAD_SETTLE_FRAMES, cancellationToken: ct);

            // The capture has no ReleaseMemorySystem, so nothing evicts the asset bundle, LOD, road and texture
            // caches; across a full-city run they filled GPU memory and crashed the player. Flush them, unbudgeted,
            // once the chunk's scenes have released their references.
            runtime.StaticContainer.CacheCleaner.UnloadCache(false);
            await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: ct);
        }

        private void WriteBlock(Vector2Int blockMin, byte[] image, JArray pending, JArray failed, JArray blocks, Action onBlock, Action onComplete)
        {
            string file = args.ClientMap ? args.TileName(blockMin) : $"{blockMin.x}_{blockMin.y}.{(args.JpegQuality.HasValue ? "jpg" : "png")}";
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

            // Production info: the production log matrix drops ENGINE, and a build run is followed through this line.
            ReportHub.LogProductionInfo($"[MapCapture] block ({blockMin.x},{blockMin.y}) written to {file}; pending {pending.Count}, failed {failed.Count}");
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

            ReportHub.LogProductionInfo($"[MapCapture] Load at ({parcels[0].x},{parcels[0].y}) TIMED OUT after {args.LoadTimeoutSec:0}s with {pending.Count} parcels pending:\n{string.Join("\n", pending.GetRange(0, Mathf.Min(pending.Count, TIMEOUT_REPORT_LIMIT)))}");
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
