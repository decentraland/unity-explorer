using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using DCL.SyntheticInput.Components;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    public class LookAtTool : McpTool
    {
        /// <summary>Residual aim above which the camera visibly did not reach the point and the driver must be told.</summary>
        private const float RESIDUAL_AIM_WARNING_DEGREES = 2f;

        private readonly SyntheticInputAgent syntheticInput;
        private readonly ExposedCameraData exposedCameraData;

        public override string Name => "look_at";

        public override string Description =>
            "Rotate the camera to look at a world-space point (x,y,z in meters). Useful to center something on screen before "
            + "a screenshot, or to aim the reticle at a target before press_input. The result reports aimErrorDegrees: the "
            + "angle still between the camera's forward and the point — normally ~0, but a third-person camera cannot pitch "
            + "past its clamp, so a steeply elevated target reports a residual instead of pretending it is centered.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("x", isRequired: true)
                  .Number("y", isRequired: true)
                  .Number("z", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public LookAtTool(SyntheticInputAgent syntheticInput, ExposedCameraData exposedCameraData)
        {
            this.syntheticInput = syntheticInput;
            this.exposedCameraData = exposedCameraData;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("x", out float x) || !arguments.TryGetFloat("y", out float y) || !arguments.TryGetFloat("z", out float z))
                return McpToolResult.Error("x, y and z world coordinates are required.");

            SyntheticInputDelivery delivery = await syntheticInput.LookAtAsync(new Vector3(x, y, z), ct);

            if (delivery == SyntheticInputDelivery.TimedOut)
                return McpToolResult.Error("look_at was not applied by the camera (is the simulation paused?).");

            // The exposed camera data is refreshed by its own system; give it one frame to observe the rotation.
            await UniTask.DelayFrame(1, cancellationToken: ct);

            Vector3 cameraPosition = exposedCameraData.WorldPosition.Value;
            Quaternion cameraRotation = exposedCameraData.WorldRotation.Value;
            var target = new Vector3(x, y, z);

            float aimErrorDegrees = Vector3.Angle(cameraRotation * Vector3.forward, target - cameraPosition);

            var result = new JObject
            {
                ["cameraPosition"] = cameraPosition.ToVector(),
                ["cameraRotationEuler"] = cameraRotation.eulerAngles.ToVector(),
                ["aimErrorDegrees"] = Math.Round(aimErrorDegrees, 1),
            };

            if (aimErrorDegrees > RESIDUAL_AIM_WARNING_DEGREES)
                result["warning"] = "the camera stopped short of the point (a rig limit, e.g. the third-person pitch clamp): "
                                    + "back away so the target sits at a shallower angle, or use set_camera_mode first_person / set_camera_pose";

            return McpToolResult.Json(result);
        }
    }
}
