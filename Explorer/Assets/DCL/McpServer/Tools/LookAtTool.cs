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
            "Rotate the camera to look at a world-space point (x,y,z in meters). Useful to center something on screen before a screenshot.";

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

            var result = new JObject
            {
                ["cameraPosition"] = exposedCameraData.WorldPosition.Value.ToVector(),
                ["cameraRotationEuler"] = exposedCameraData.WorldRotation.Value.eulerAngles.ToVector(),
            };

            return McpToolResult.Json(result);
        }
    }
}
