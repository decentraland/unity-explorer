using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Captures the current frame through the shared <see cref="ScreenCaptureService" /> and returns it as an
    ///     MCP image content block. Waits out a Cinemachine blend first, so the frame is the settled view rather
    ///     than a mid-transition one.
    /// </summary>
    public class ScreenshotTool : McpTool
    {
        /// <summary>Longest a blend is waited out; the standard blends run 0.5–1 s.</summary>
        private const float CAMERA_BLEND_DEADLINE_SEC = 1.5f;

        private readonly ScreenCaptureService capture;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IExposedCameraData exposedCameraData;

        public override string Name => "screenshot";

        public override string Description =>
            "Capture a screenshot of what the player currently sees in the Explorer, including scene UI. "
            + "Use worldOnly to exclude all UI overlays. Returns a downscaled image plus a caption with the capture context "
            + "(the screen size when downscaled, and how long a camera blend was waited out). One capture runs at a time: "
            + "a call issued while a screenshot or capture_sequence is in progress is refused, not queued.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("maxWidth", "Maximum output width in pixels (aspect ratio preserved). Default 1280.")
                  .Enum<ScreenCaptureService.Format>("quality", "Output encoding. Default jpg.")
                  .Boolean("worldOnly", "Render only the 3D world through the main camera, excluding UI. Default false.")
                  .Boolean("waitForCameraBlend", "Wait (at most 1.5 s) for a Cinemachine camera blend to finish before capturing, so the frame is not "
                                                 + "mid-transition after an SDK virtual camera hands back or a mode switch. Default true.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public ScreenshotTool(ScreenCaptureService capture, World world, Entity playerEntity, IExposedCameraData exposedCameraData)
        {
            this.capture = capture;
            this.world = world;
            this.playerEntity = playerEntity;
            this.exposedCameraData = exposedCameraData;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            int maxWidth = Mathf.Clamp(arguments.GetInt("maxWidth", ScreenCaptureService.DEFAULT_MAX_WIDTH), ScreenCaptureService.MIN_WIDTH, ScreenCaptureService.MAX_WIDTH);

            if (!arguments.TryGetEnum("quality", ScreenCaptureService.Format.Jpg, out ScreenCaptureService.Format format))
                return McpToolResult.Error(arguments.EnumArgumentError<ScreenCaptureService.Format>("quality"));

            bool worldOnly = arguments.GetBool("worldOnly", false);
            bool waitForCameraBlend = arguments.GetBool("waitForCameraBlend", true);

            if (!capture.TryAcquire(Name))
                return McpToolResult.Error($"A capture is already in progress ({capture.BusyOwner}); retry when it completes.");

            try
            {
                (int waitedMs, bool stillBlending) = waitForCameraBlend
                    ? await CameraBlendWait.WaitAsync(exposedCameraData, CAMERA_BLEND_DEADLINE_SEC, ct)
                    : (0, false);

                Vector2Int parcel = world.Get<CharacterTransform>(playerEntity).Position.ToParcel();

                ScreenCaptureService.Frame frame = await capture.CaptureAsync(maxWidth, format, worldOnly, ct);

                string scale = frame.Width != frame.ScreenWidth ? $" (screen {frame.ScreenWidth}x{frame.ScreenHeight})" : string.Empty;
                var caption = $"{frame.Width}x{frame.Height}{scale} {(worldOnly ? "world-only" : "full-view")} capture at parcel ({parcel.x},{parcel.y})";

                if (stillBlending)
                    caption += $", camera still blending after {(int)(CAMERA_BLEND_DEADLINE_SEC * 1000f)} ms — the frame may be mid-transition";
                else if (waitedMs > 0)
                    caption += $", waited {waitedMs} ms for the camera blend";

                return McpToolResult.Image(frame.Bytes, frame.MimeType, caption);
            }
            finally { capture.Release(Name); }
        }
    }
}
