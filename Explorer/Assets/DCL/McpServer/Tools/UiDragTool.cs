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
    ///     fallback to the device path also reports why the semantic one did not apply, so a caller who meant to
    ///     drag the scene's UI can tell the two apart. The device path additionally reports what its pointer was
    ///     over at each end: the gesture verifies no target, so a bare success would read as a delivered drag even
    ///     when the pointer was over the world and no UI could have received it.
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

        /// <summary>What `pointerOver` reads when no UI covered that pixel — the drag ran over the 3D world there.</summary>
        private const string WORLD = "world";

        private const int DEFAULT_DURATION_FRAMES = 15;
        private const int MIN_DURATION_FRAMES = 2;
        private const int MAX_DURATION_FRAMES = 300;

        private readonly UiAutomationServices uiAutomation;

        public override string Name => "ui_drag";

        public override string Description =>
            "Press, drag and release between two screen positions given as normalized image coordinates (x right 0..1, "
            + "y DOWN 0..1, origin top-left — the same way you read a screenshot). A drag whose start point lands on the "
            + "scene's own UI is delivered to those elements (press on the start element, release on the end element); "
            + "anywhere else it replays the virtual mouse through the real input pipeline (drag thresholds, hit-testing), "
            + "e.g. to drag client list items, sliders or map panning. The result's path says which one ran, plus a "
            + "pathReason when the scene UI was not usable and the virtual mouse took over. On the mouse path read "
            + "pointerOver: it names what covered each end of the drag, or 'world' when nothing did — a drag over the "
            + "world reaches no UI at all (sweep a held pointer across the world with sweep_pointer instead), and ok "
            + "there means only that the mouse states were replayed. Pass path:sdk to fail instead of falling back, "
            + "or path:device to force the mouse.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("fromX", "Normalized start x, 0 (left) to 1 (right).", isRequired: true)
                  .Number("fromY", "Normalized start y, 0 (top) to 1 (bottom).", isRequired: true)
                  .Number("toX", "Normalized end x.", isRequired: true)
                  .Number("toY", "Normalized end y.", isRequired: true)
                  .Integer("durationFrames", "Frames spent moving between the points. Default 15, max 300.")
                  .Boolean("rightButton", "Drag with the right button instead of the left. Default false.")
                  .Enum<DragPath>("path", "Which delivery path to use. Default auto (the scene's UI when the start point "
                                          + "lands on it, the virtual mouse otherwise). sdk fails if the scene's UI does not own "
                                          + "the start point, instead of replaying the mouse over the world behind it; device "
                                          + "forces the mouse.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public UiDragTool(UiAutomationServices uiAutomation)
        {
            this.uiAutomation = uiAutomation;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("fromX", out float fromX) || !arguments.TryGetFloat("fromY", out float fromY)
                || !arguments.TryGetFloat("toX", out float toX) || !arguments.TryGetFloat("toY", out float toY))
                return McpToolResult.Error("fromX, fromY, toX and toY normalized image coordinates are required." + arguments.NonNumericHint("fromX", "fromY", "toX", "toY"));

            if (fromX is < 0f or > 1f || fromY is < 0f or > 1f || toX is < 0f or > 1f || toY is < 0f or > 1f)
                return McpToolResult.Error("coordinates must be normalized image values in [0, 1].");

            if (!arguments.TryGetEnum("path", DragPath.Auto, out DragPath path))
                return McpToolResult.Error("path must be one of: auto, sdk, device.");

            int durationFrames = Mathf.Clamp(arguments.TryGetInt("durationFrames", out int frames) ? frames : DEFAULT_DURATION_FRAMES, MIN_DURATION_FRAMES, MAX_DURATION_FRAMES);

            string? skippedSceneUi = null;

            if (path != DragPath.Device)
            {
                Vector2 fromImage = UiScreenGeometry.NormalizedToImagePoint(new Vector2(fromX, fromY));
                Vector2 toImage = UiScreenGeometry.NormalizedToImagePoint(new Vector2(toX, toY));

                SceneUiDragAttempt attempt = await uiAutomation.DragSceneUiAsync(fromImage, toImage, durationFrames, ct);

                if (attempt.Result.HasValue)
                {
                    JObject sdkJson = attempt.Result.Value.ToJson(uiAutomation.CursorStateName());
                    sdkJson["path"] = McpWireEnum<DragPath>.ToWire(DragPath.Sdk);
                    return McpToolResult.Json(sdkJson);
                }

                skippedSceneUi = attempt.SkipReason;

                // The caller pinned the semantic path: falling back would replay the mouse over the world instead.
                if (path == DragPath.Sdk)
                    return McpToolResult.Error($"the drag was not delivered to the scene's UI: {skippedSceneUi}");
            }

            Vector2 from = UiScreenGeometry.NormalizedImageToScreenPoint(new Vector2(fromX, fromY));
            Vector2 to = UiScreenGeometry.NormalizedImageToScreenPoint(new Vector2(toX, toY));

            MouseButton button = arguments.GetBool("rightButton", false) ? MouseButton.Right : MouseButton.Left;

            UiDeviceDragOutcome outcome = await uiAutomation.DragWithDevicesAsync(from, to, durationFrames, button, ct);

            if (!outcome.Ok)
                return McpToolResult.Error(outcome.FailureReason ?? "the drag failed");

            var result = new JObject
            {
                ["ok"] = true,
                ["path"] = McpWireEnum<DragPath>.ToWire(DragPath.Device),
                ["cursorState"] = uiAutomation.CursorStateName(),

                // What the pointer was over at each end. A device gesture verifies no target, so this is the only
                // thing that separates a drag some UI could have received from one replayed over the world.
                ["pointerOver"] = new JObject
                {
                    ["start"] = outcome.CoverAtStart ?? WORLD,
                    ["end"] = outcome.CoverAtEnd ?? WORLD,
                },

                // The space the from/to coordinates were normalized against, stated like every other UI result.
                ["screen"] = new JObject { ["width"] = Screen.width, ["height"] = Screen.height },
            };

            if (outcome.DeliveryNote != null)
                result["info"] = outcome.DeliveryNote;

            // The states were replayed, so ok is true — but the semantic path was what a caller aiming at scene UI
            // asked for. Naming the reason is what separates this from a delivered UI drag.
            if (skippedSceneUi != null)
                result["pathReason"] = $"the scene-UI path did not apply ({skippedSceneUi}), so the virtual mouse was replayed instead";

            return McpToolResult.Json(result);
        }
    }
}
