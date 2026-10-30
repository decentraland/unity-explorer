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
    public class LookAtTool : McpTool
    {
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
                return McpToolResult.Error("x, y and z world coordinates are required." + arguments.NonNumericHint("x", "y", "z"));

            SyntheticInputDelivery delivery = await syntheticInput.LookAtAsync(new Vector3(x, y, z), ct);

            if (delivery == SyntheticInputDelivery.TimedOut)
                return McpToolResult.Error("look_at was not applied by the camera (is the simulation paused?).");

            (Vector3 cameraPosition, Quaternion cameraRotation) = await exposedCameraData.ReadSettledPoseAsync(ct);

            var result = new JObject
            {
                ["cameraPosition"] = cameraPosition.ToVector(),
            };

            CameraAimReport.Append(result, cameraPosition, cameraRotation, new Vector3(x, y, z));

            return McpToolResult.Json(result);
        }
    }
}
