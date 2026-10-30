using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Captures a burst of frames at a true interval, in-process, and writes them to a server-owned directory.
    ///     A round trip per frame through <c>screenshot</c> cannot reach sub-second cadence; this can, and it keeps
    ///     the frames out of the response so a burst costs the agent no image context.
    /// </summary>
    public class CaptureSequenceTool : McpTool
    {
        /// <summary>Shape of <see cref="ScreenCaptureService.CaptureAsync" />, injectable so tests run without GPU work.</summary>
        internal delegate UniTask<ScreenCaptureService.Frame> CaptureFrame(int maxWidth, ScreenCaptureService.Format format, bool worldOnly, CancellationToken ct);

        public const int DEFAULT_COUNT = 5;
        public const int MAX_COUNT = 20;
        public const float DEFAULT_INTERVAL_SEC = 0.5f;
        public const float MIN_INTERVAL_SEC = 0.05f;
        public const float MAX_INTERVAL_SEC = 2f;
        public const int DEFAULT_MAX_WIDTH = 640;

        private const string CAPTURES_FOLDER = "mcp-captures";
        private static readonly TimeSpan PRUNE_AGE = TimeSpan.FromHours(24);

        private readonly ScreenCaptureService capture;
        private readonly CaptureFrame captureFrame;
        private readonly World world;
        private readonly Entity playerEntity;

        public override string Name => "capture_sequence";

        public override string Description =>
            "Capture count frames intervalSec apart, in-process at the true interval (down to 0.05 s), for events a single "
            + "screenshot cannot catch: a flash, a burst, a transition. The frames are written as files under the Explorer's own "
            + "temporary cache directory (never under a scene folder) and the result lists their paths, sizes and timestamps — no "
            + "image is returned inline, so read only the frames you need with a file tool. Shares the capture gate with screenshot: "
            + "a call issued while either runs is refused. Defaults to 640 px wide so a burst stays cheap.";

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .String("directory", "Directory the frames were written to.")
                          .Integer("count", "Frames captured.")
                          .Number("requestedIntervalSec")
                          .Number("meanIntervalSec", "Mean time between consecutive frames, from their end-of-frame timestamps; null for a single frame.", nullable: true)
                          .Integer("late", "Frames whose capture and encode outlasted the interval, so the next frame started late.")
                          .Object("screen", McpJsonSchema.Object().Integer("width").Integer("height"), "The screen size the frames were downscaled from.")
                          .ObjectArray("frames", McpJsonSchema.Object()
                                                               .Integer("index")
                                                               .String("path")
                                                               .Number("stamp", "Seconds since the client started, at the end of the frame captured.")
                                                               .Integer("width")
                                                               .Integer("height")
                                                               .Number("intervalFromPreviousSec", nullable: true))
                          .String("note")
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public CaptureSequenceTool(ScreenCaptureService capture, World world, Entity playerEntity)
            : this(capture, capture.CaptureAsync, world, playerEntity) { }

        internal CaptureSequenceTool(ScreenCaptureService capture, CaptureFrame captureFrame, World world, Entity playerEntity)
        {
            this.capture = capture;
            this.captureFrame = captureFrame;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("count", $"Frames to capture, 1..{MAX_COUNT}. Default {DEFAULT_COUNT}.")
                  .Number("intervalSec", $"Seconds between frames, {MIN_INTERVAL_SEC}..{MAX_INTERVAL_SEC}. Default {DEFAULT_INTERVAL_SEC}.")
                  .Integer("maxWidth", $"Maximum frame width in pixels (aspect ratio preserved). Default {DEFAULT_MAX_WIDTH}.")
                  .Enum<ScreenCaptureService.Format>("quality", "Frame encoding. Default jpg.")
                  .Boolean("worldOnly", "Render only the 3D world through the main camera, excluding UI. Default false.");

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            (int count, float intervalSec) = ClampArguments(arguments);
            int maxWidth = Mathf.Clamp(arguments.GetInt("maxWidth", DEFAULT_MAX_WIDTH), ScreenCaptureService.MIN_WIDTH, ScreenCaptureService.MAX_WIDTH);

            if (!arguments.TryGetEnum("quality", ScreenCaptureService.Format.Jpg, out ScreenCaptureService.Format format))
                return McpToolResult.Error(arguments.EnumArgumentError<ScreenCaptureService.Format>("quality"));

            bool worldOnly = arguments.GetBool("worldOnly", false);

            if (!capture.TryAcquire(Name))
                return McpToolResult.Error($"A capture is already in progress ({capture.BusyOwner}); retry when it completes.");

            try
            {
                string capturesRoot = Path.Combine(Application.temporaryCachePath, CAPTURES_FOLDER);
                string directory = Path.Combine(capturesRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
                string extension = format == ScreenCaptureService.Format.Png ? "png" : "jpg";

                PruneOldCaptures(capturesRoot);
                Directory.CreateDirectory(directory);

                Vector2Int parcel = world.Get<CharacterTransform>(playerEntity).Position.ToParcel();

                var frames = new JArray();
                var late = 0;
                var intervalSum = 0.0;
                float previousStamp = 0f;
                var screenWidth = 0;
                var screenHeight = 0;

                for (var index = 0; index < count; index++)
                {
                    ScreenCaptureService.Frame frame = await captureFrame(maxWidth, format, worldOnly, ct);

                    // The capture returns on the thread pool; the bookkeeping below reads Unity time, so it runs on the main thread.
                    await UniTask.SwitchToMainThread(ct);

                    string path = Path.Combine(directory, $"frame-{index:000}.{extension}");
                    File.WriteAllBytes(path, frame.Bytes);

                    screenWidth = frame.ScreenWidth;
                    screenHeight = frame.ScreenHeight;

                    var entry = new JObject
                    {
                        ["index"] = index,
                        ["path"] = path,
                        ["stamp"] = Math.Round(frame.RealtimeStamp, 3),
                        ["width"] = frame.Width,
                        ["height"] = frame.Height,
                        ["intervalFromPreviousSec"] = index == 0 ? JValue.CreateNull() : Math.Round(frame.RealtimeStamp - previousStamp, 3),
                    };

                    if (index > 0)
                        intervalSum += frame.RealtimeStamp - previousStamp;

                    previousStamp = frame.RealtimeStamp;
                    frames.Add(entry);

                    if (index == count - 1)
                        continue;

                    // The next frame is scheduled from the captured frame's stamp, not from now, so encode time does not accumulate.
                    float remaining = frame.RealtimeStamp + intervalSec - UnityEngine.Time.realtimeSinceStartup;

                    if (remaining <= 0f)
                        late++;
                    else
                        await UniTask.Delay(TimeSpan.FromSeconds(remaining), DelayType.Realtime, PlayerLoopTiming.Update, ct);
                }

                var structured = new JObject
                {
                    ["directory"] = directory,
                    ["count"] = count,
                    ["requestedIntervalSec"] = intervalSec,
                    ["meanIntervalSec"] = count > 1 ? Math.Round(intervalSum / (count - 1), 3) : JValue.CreateNull(),
                    ["late"] = late,
                    ["screen"] = new JObject { ["width"] = screenWidth, ["height"] = screenHeight },
                    ["frames"] = frames,
                    ["note"] = $"{count} {(worldOnly ? "world-only" : "full-view")} frame(s) at parcel ({parcel.x},{parcel.y}); frames are files, read only the ones you need. "
                               + "Directories older than 24 h under the captures folder are pruned on the next call.",
                };

                return McpToolResult.JsonWithStructured(structured);
            }
            finally { capture.Release(Name); }
        }

        /// <summary>The count and interval the call will use, clamped to the documented ranges.</summary>
        internal static (int count, float intervalSec) ClampArguments(JObject arguments) =>
            (Mathf.Clamp(arguments.GetInt("count", DEFAULT_COUNT), 1, MAX_COUNT),
                Mathf.Clamp(arguments.GetFloat("intervalSec", DEFAULT_INTERVAL_SEC), MIN_INTERVAL_SEC, MAX_INTERVAL_SEC));

        private static void PruneOldCaptures(string capturesRoot)
        {
            try
            {
                if (!Directory.Exists(capturesRoot))
                    return;

                DateTime cutoff = DateTime.UtcNow - PRUNE_AGE;

                foreach (string old in Directory.GetDirectories(capturesRoot))
                    if (Directory.GetLastWriteTimeUtc(old) < cutoff)
                        Directory.Delete(old, recursive: true);
            }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.MCP); }
        }
    }
}
