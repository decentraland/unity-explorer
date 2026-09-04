using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
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
    ///     Holds a relative camera-look input via <see cref="SyntheticInputAgent.CameraLookAsync" />, feeding the
    ///     same Cinemachine input axes mouse-look feeds. Use look_at for an absolute aim; this tool is for
    ///     human-like relative turns (e.g. panning across a scene).
    /// </summary>
    public class CameraLookTool : McpTool
    {
        private const float DEFAULT_SECONDS = 0.5f;
        private const float MIN_SECONDS = 0.05f;
        private const float MAX_SECONDS = 10f;
        private const float MAX_AXIS = 50f;

        private readonly SyntheticInputAgent syntheticInput;
        private readonly ExposedCameraData exposedCameraData;

        public override string Name => "camera_look";

        public override string Description =>
            "Turn the camera with a held relative look input, like moving the mouse: deltaX turns right (+) or left (-), "
            + "deltaY looks up (+) or down (-), in mouse-delta units per frame (2-10 is a gentle turn). The camera keeps "
            + "turning for the given seconds. Blocked while a UI has camera focus, exactly like real mouse-look. "
            + "Use look_at to aim at a known world point instead.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("deltaX", "Horizontal look speed in mouse-delta units per frame: positive turns right.", isRequired: true)
                  .Number("deltaY", "Vertical look speed in mouse-delta units per frame: positive looks up.", isRequired: true)
                  .Number("seconds", "How long to hold the look input. Default 0.5, max 10.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public CameraLookTool(SyntheticInputAgent syntheticInput, ExposedCameraData exposedCameraData)
        {
            this.syntheticInput = syntheticInput;
            this.exposedCameraData = exposedCameraData;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("deltaX", out float deltaX) || !arguments.TryGetFloat("deltaY", out float deltaY))
                return McpToolResult.Error("deltaX and deltaY are required." + arguments.NonNumericHint("deltaX", "deltaY"));

            if (deltaX == 0f && deltaY == 0f)
                return McpToolResult.Error("deltaX and deltaY must not both be zero.");

            var axisValue = new Vector2(Mathf.Clamp(deltaX, -MAX_AXIS, MAX_AXIS), Mathf.Clamp(deltaY, -MAX_AXIS, MAX_AXIS));
            float seconds = Mathf.Clamp(arguments.GetFloat("seconds", DEFAULT_SECONDS), MIN_SECONDS, MAX_SECONDS);

            SyntheticInputDelivery delivery = await syntheticInput.CameraLookAsync(axisValue, seconds, ct);

            if (delivery == SyntheticInputDelivery.TimedOut)
                return McpToolResult.Error($"camera_look did not complete within {seconds + SyntheticInputAgent.COMPLETION_GRACE_SEC}s (is the simulation paused?).");

            // The exposed camera data is refreshed by its own system; give it one frame to observe the rotation.
            await UniTask.DelayFrame(1, cancellationToken: ct);

            var result = new JObject
            {
                ["cameraRotationEuler"] = exposedCameraData.WorldRotation.Value.eulerAngles.ToVector(),
            };

            return McpToolResult.Json(result);
        }
    }
}
