using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using DCL.SyntheticInput.Components;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     The held-and-turn gesture, composed by <see cref="SyntheticInputAgent.SweepAsync" /> from a press, a
    ///     camera-look hold and a release. This is the only way a driver can sweep the pointer ray a scene samples
    ///     (<c>PrimaryPointerInfo.WorldRayDirection</c>, the ray through the pointer's pixel, which the press
    ///     parks on the target while the camera turns under it): dragging the virtual mouse across the world pans
    ///     the camera instead, exactly as a human's held-button drag does.
    /// </summary>
    public class SweepPointerTool : McpTool
    {
        private const float DEFAULT_SECONDS = 1f;
        private const float MIN_SECONDS = 0.05f;
        private const float MAX_SECONDS = 10f;
        private const float MAX_AXIS = 50f;
        private const float DEFAULT_TIMEOUT_SEC = 3f;
        private const float MIN_TIMEOUT_SEC = 0.5f;
        private const float MAX_TIMEOUT_SEC = 15f;

        private readonly SyntheticInputAgent syntheticInput;
        private readonly ExposedCameraData exposedCameraData;

        public override string Name => "sweep_pointer";

        public override string Description =>
            "Press a pointer button on a scene entity, turn the camera while it is held, then release — the gesture a "
            + "human makes to drag a pointer across the world (painting, dragging a held target, sweeping a ray). The "
            + "press arms scenes that watch for a pointer-down and parks the pointer on the target, and the camera turn "
            + "is what then drags the ray a scene reads from PrimaryPointerInfo across the world; deltaX/deltaY/seconds "
            + "behave exactly as in camera_look. Check pressed.hit: a press that landed on nothing armed nothing, and "
            + "the sweep then turned the camera with nothing held. Point the camera at the target first (look_at): only "
            + "a press that lands on screen parks the pointer, and a sweep with no parked pointer turns the camera "
            + "without dragging anything. Use click_entity for a click in place, and ui_drag for dragging inside UI.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("deltaX", "Horizontal look speed while the button is held, in mouse-delta units per frame: positive turns right.", isRequired: true)
                  .Number("deltaY", "Vertical look speed while the button is held: positive looks up.", isRequired: true)
                  .Number("seconds", "How long the button is held while the camera turns. Default 1, max 10.")
                  .Integer("entityId", "Entity to press on (from list_scene_entities). Omit only when x/y/z are given, then the ray decides the target.")
                  .Number("x", "World-space aim point for the press; overrides the automatic aim at the entity's collider center.")
                  .Number("y")
                  .Number("z")
                  .String("sceneId", "Pin the gesture to this scene (id from get_scene_state): it fails instead of landing in another scene if the player moved.")
                  .Enum<PointerButton>("button", "Which input action to hold. Default pointer (left click / IA_POINTER).")
                  .Number("timeoutSec", "Seconds to wait for each of the press and release. Default 3, max 15.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public SweepPointerTool(SyntheticInputAgent syntheticInput, ExposedCameraData exposedCameraData)
        {
            this.syntheticInput = syntheticInput;
            this.exposedCameraData = exposedCameraData;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("deltaX", out float deltaX) || !arguments.TryGetFloat("deltaY", out float deltaY))
                return McpToolResult.Error("deltaX and deltaY are required." + arguments.NonNumericHint("deltaX", "deltaY"));

            if (deltaX == 0f && deltaY == 0f)
                return McpToolResult.Error("deltaX and deltaY must not both be zero: a sweep that does not turn the camera is a click_entity down/up pair.");

            // The press needs a target to arm on, so the aim is mandatory here.
            if (!PointerArgs.TryParseAim(arguments, requireTarget: true, out PointerAim aim, out string? aimError))
                return McpToolResult.Error(aimError!);

            if (!PointerArgs.TryGetButton(arguments, out InputAction button, out string? buttonError))
                return McpToolResult.Error(buttonError!);

            var axisValue = new Vector2(Mathf.Clamp(deltaX, -MAX_AXIS, MAX_AXIS), Mathf.Clamp(deltaY, -MAX_AXIS, MAX_AXIS));
            float seconds = Mathf.Clamp(arguments.GetFloat("seconds", DEFAULT_SECONDS), MIN_SECONDS, MAX_SECONDS);
            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);

            SyntheticSweepResult sweep = await syntheticInput.SweepAsync(aim, button, axisValue, seconds, timeoutSec, ct);

            var json = new JObject
            {
                // Each leg is shaped like click_entity's result, so the same fields mean the same things.
                ["pressed"] = sweep.Press.ToJson(),
            };

            if (sweep.FailureReason != null)
            {
                json["swept"] = false;
                json["reason"] = sweep.FailureReason;
                return McpToolResult.Json(json);
            }

            json["swept"] = sweep.CameraSweep == SyntheticInputDelivery.Completed;

            if (sweep.CameraSweep != SyntheticInputDelivery.Completed)
                json["sweepReason"] = sweep.CameraSweep == SyntheticInputDelivery.TimedOut
                    ? $"the camera hold did not complete within {seconds + SyntheticInputAgent.COMPLETION_GRACE_SEC}s (is the simulation paused?)"
                    : "a newer camera request replaced the sweep before it finished";

            // The exposed camera data is refreshed by its own system; give it one frame to observe the rotation.
            await UniTask.DelayFrame(1, cancellationToken: ct);

            json["cameraRotationEuler"] = exposedCameraData.WorldRotation.Value.eulerAngles.ToVector();
            json["released"] = sweep.Release.ToJson();

            return McpToolResult.Json(json);
        }
    }
}
