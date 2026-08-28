using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Drags between two screen points. Inside the SDK scene UI the drag is synthesized semantically — the
    ///     element under the start point receives the press, the elements along the path the moves, the element
    ///     under the end point the release — because UI Toolkit panels consume events sent to their elements
    ///     rather than virtual-device pointer state. Elsewhere (and whenever <c>device</c> is set) the virtual
    ///     mouse is replayed instead, which is the path that exercises real drag thresholds and hit-testing.
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
            "Press, drag and release between two screen positions given as normalized image coordinates (x right 0..1, "
            + "y DOWN 0..1, origin top-left — the same way you read a screenshot). A drag whose start point lands on the "
            + "scene's own UI is delivered to those elements (press on the start element, release on the end element); "
            + "anywhere else it replays the virtual mouse through the real input pipeline (drag thresholds, hit-testing), "
            + "e.g. to drag client list items, sliders or map panning. Set device:true to force the virtual mouse.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("fromX", "Normalized start x, 0 (left) to 1 (right).", isRequired: true)
                  .Number("fromY", "Normalized start y, 0 (top) to 1 (bottom).", isRequired: true)
                  .Number("toX", "Normalized end x.", isRequired: true)
                  .Number("toY", "Normalized end y.", isRequired: true)
                  .Integer("durationFrames", "Frames spent moving between the points. Default 15, max 300.")
                  .Boolean("rightButton", "Drag with the right button instead of the left. Default false.")
                  .Boolean("device", "Force the virtual-mouse path even when the drag starts on the scene's own UI. Default false.");

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

            var fromImage = new Vector2(fromX * Screen.width, fromY * Screen.height);
            var toImage = new Vector2(toX * Screen.width, toY * Screen.height);

            if (!arguments.GetBool("device", false))
            {
                UiActionResult? sceneUiDrag = await uiAutomation.TryDragSceneUiAsync(fromImage, toImage, durationFrames, ct);

                if (sceneUiDrag.HasValue)
                {
                    JObject sdkJson = sceneUiDrag.Value.ToJson(uiAutomation.CursorStateName());
                    sdkJson["path"] = "sdk";
                    return McpToolResult.Json(sdkJson);
                }
            }

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
                ["path"] = "device",
                ["cursorState"] = uiAutomation.CursorStateName(),
            };

            return McpToolResult.Json(result);
        }
    }
}
