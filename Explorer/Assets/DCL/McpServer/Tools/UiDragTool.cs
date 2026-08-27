using Cysharp.Threading.Tasks;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Drags the virtual mouse between two screen points with the button held — the device path is the only
    ///     one that exercises real drag thresholds and hit-testing, so there is no semantic variant of this tool.
    /// </summary>
    public class UiDragTool : McpTool
    {
        private const int DEFAULT_DURATION_FRAMES = 15;
        private const int MIN_DURATION_FRAMES = 2;
        private const int MAX_DURATION_FRAMES = 300;
        private const float TIMEOUT_GRACE_SEC = 5f;
        private const float ASSUMED_MIN_FPS = 15f;

        private readonly UiAutomationServices uiAutomation;

        public override string Name => "ui_drag";

        public override string Description =>
            "Press, drag and release the virtual mouse between two screen positions given as normalized image coordinates "
            + "(x right 0..1, y DOWN 0..1, origin top-left — the same way you read a screenshot). Runs through the real "
            + "input pipeline (drag thresholds, hit-testing), e.g. to drag list items, sliders or map panning.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("fromX", "Normalized start x, 0 (left) to 1 (right).", isRequired: true)
                  .Number("fromY", "Normalized start y, 0 (top) to 1 (bottom).", isRequired: true)
                  .Number("toX", "Normalized end x.", isRequired: true)
                  .Number("toY", "Normalized end y.", isRequired: true)
                  .Integer("durationFrames", "Frames spent moving between the points. Default 15, max 300.")
                  .Boolean("rightButton", "Drag with the right button instead of the left. Default false.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public UiDragTool(UiAutomationServices uiAutomation)
        {
            this.uiAutomation = uiAutomation;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("fromX", out float fromX) || !arguments.TryGetFloat("fromY", out float fromY)
                || !arguments.TryGetFloat("toX", out float toX) || !arguments.TryGetFloat("toY", out float toY))
                return McpToolResult.Error("fromX, fromY, toX and toY normalized image coordinates are required.");

            if (fromX is < 0f or > 1f || fromY is < 0f or > 1f || toX is < 0f or > 1f || toY is < 0f or > 1f)
                return McpToolResult.Error("coordinates must be normalized image values in [0, 1].");

            int durationFrames = Mathf.Clamp(arguments.TryGetInt("durationFrames", out int frames) ? frames : DEFAULT_DURATION_FRAMES, MIN_DURATION_FRAMES, MAX_DURATION_FRAMES);

            var from = new Vector2(fromX * Screen.width, (1f - fromY) * Screen.height);
            var to = new Vector2(toX * Screen.width, (1f - toY) * Screen.height);

            float timeoutSec = (durationFrames / ASSUMED_MIN_FPS) + TIMEOUT_GRACE_SEC;

            UiGestureResult gesture = await uiAutomation.RunGestureAsync(new UiDeviceGestureRequest
            {
                Kind = UiDeviceGestureKind.Drag,
                From = from,
                To = to,
                DurationFrames = durationFrames,
                Button = arguments.GetBool("rightButton", false) ? MouseButton.Right : MouseButton.Left,
            }, timeoutSec, ct);

            if (!gesture.Ok)
                return McpToolResult.Error(gesture.FailureReason ?? "the drag failed");

            var result = new JObject
            {
                ["ok"] = true,
                ["cursorState"] = uiAutomation.CursorStateName(),
            };

            return McpToolResult.Json(result);
        }
    }
}
