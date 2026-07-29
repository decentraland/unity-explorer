using Arch.Core;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    public class LookAtTool : McpTool
    {
        private readonly IGlobalWorldActions globalWorldActions;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly ExposedCameraData exposedCameraData;

        public override string Name => "look_at";

        public override string Description =>
            "Rotate the camera to look at a world-space point (x,y,z in meters). Useful to center something on screen before a screenshot.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("x", isRequired: true)
                  .Number("y", isRequired: true)
                  .Number("z", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public LookAtTool(IGlobalWorldActions globalWorldActions, World world, Entity playerEntity, ExposedCameraData exposedCameraData)
        {
            this.globalWorldActions = globalWorldActions;
            this.world = world;
            this.playerEntity = playerEntity;
            this.exposedCameraData = exposedCameraData;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("x", out float x) || !arguments.TryGetFloat("y", out float y) || !arguments.TryGetFloat("z", out float z))
                return McpToolResult.Error("x, y and z world coordinates are required.");

            Vector3 playerPosition = world.Get<CharacterTransform>(playerEntity).Position;
            globalWorldActions.RotateCamera(new Vector3(x, y, z), playerPosition);

            // Let the Cinemachine systems apply the look-at intent before reading the camera back.
            await UniTask.DelayFrame(3, cancellationToken: ct);

            var result = new JObject
            {
                ["cameraPosition"] = exposedCameraData.WorldPosition.Value.ToVector(),
                ["cameraRotationEuler"] = exposedCameraData.WorldRotation.Value.eulerAngles.ToVector(),
            };

            return McpToolResult.Json(result);
        }
    }
}
