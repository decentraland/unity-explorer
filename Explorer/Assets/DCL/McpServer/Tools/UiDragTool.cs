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
    ///     rather than virtual-device pointer state. Elsewhere the virtual mouse is replayed instead, which is the
    ///     path that exercises real drag thresholds and hit-testing. Which path ran is reported, and an automatic
    ///     fallback to the device path also reports why the semantic one did not apply: that fallback drags the 3D
    ///     world, so a caller who meant to drag the scene's UI must be able to tell the two apart.
    /// </summary>
    public class UiDragTool : McpTool
    {
        /// <summary>Which delivery path the caller allows. The default picks one and says which; the others pin it.</summary>
        private enum DragPath : byte
        {
            Auto,
            Sdk,
            Device,
        }

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
            + "e.g. to drag client list items, sliders or map panning. The result's path says which one ran, plus a "
            + "pathReason when the scene UI was not usable and the virtual mouse took over — that fallback drags the 3D "
            + "world, not the UI. Pass path:sdk to fail instead of falling back, or path:device to force the mouse.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("fromX", "Normalized start x, 0 (left) to 1 (right).", isRequired: true)
                  .Number("fromY", "Normalized start y, 0 (top) to 1 (bottom).", isRequired: true)
                  .Number("toX", "Normalized end x.", isRequired: true)
                  .Number("toY", "Normalized end y.", isRequired: true)
                  .Integer("durationFrames", "Frames spent moving between the points. Default 15, max 300.")
                  .Boolean("rightButton", "Drag with the right button instead of the left. Default false.")
                  .Enum<DragPath>("path", "Which delivery path to use. Default auto (the scene's UI when the start point "
                                          + "lands on it, the virtual mouse otherwise). sdk fails if the scene's UI does not own "
                                          + "the start point, instead of dragging the world behind it; device forces the mouse.");

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

            if (!arguments.TryGetEnum("path", DragPath.Auto, out DragPath path))
                return McpToolResult.Error("path must be one of: auto, sdk, device.");

            int durationFrames = Mathf.Clamp(arguments.TryGetInt("durationFrames", out int frames) ? frames : DEFAULT_DURATION_FRAMES, MIN_DURATION_FRAMES, MAX_DURATION_FRAMES);

            string? skippedSceneUi = null;

            if (path != DragPath.Device)
            {
                var fromImage = new Vector2(fromX * Screen.width, fromY * Screen.height);
                var toImage = new Vector2(toX * Screen.width, toY * Screen.height);

                SceneUiDragAttempt attempt = await uiAutomation.DragSceneUiAsync(fromImage, toImage, durationFrames, ct);

                if (attempt.Result.HasValue)
                {
                    JObject sdkJson = attempt.Result.Value.ToJson(uiAutomation.CursorStateName());
                    sdkJson["path"] = McpWireEnum<DragPath>.ToWire(DragPath.Sdk);
                    return McpToolResult.Json(sdkJson);
                }

                skippedSceneUi = attempt.SkipReason;

                // The caller pinned the semantic path: falling back would drag the 3D world instead of the UI.
                if (path == DragPath.Sdk)
                    return McpToolResult.Error($"the drag was not delivered to the scene's UI: {skippedSceneUi}");
            }

            // Image coordinates run top-down; Unity screen coordinates run bottom-up.
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
                ["path"] = McpWireEnum<DragPath>.ToWire(DragPath.Device),
                ["cursorState"] = uiAutomation.CursorStateName(),

                // The space the from/to coordinates were normalized against, stated like every other UI result.
                ["screen"] = new JObject { ["width"] = Screen.width, ["height"] = Screen.height },
            };

            // The device drag really happened, so ok is true — but it dragged the 3D world, which is not what a
            // caller aiming at scene UI asked for. Naming the reason is what separates this from a delivered UI drag.
            if (skippedSceneUi != null)
                result["pathReason"] = $"the scene-UI path did not apply ({skippedSceneUi}), so the virtual mouse dragged the 3D world";

            return McpToolResult.Json(result);
        }
    }
}
