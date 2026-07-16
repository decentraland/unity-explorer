using Arch.Core;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Mcp.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.Mcp.Tools
{
    public class LookAtTool : IMcpTool
    {
        private readonly IGlobalWorldActions globalWorldActions;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly ExposedCameraData exposedCameraData;

        public string Name => "look_at";

        public string Description =>
            "Rotate the camera to look at a world-space point (x,y,z in meters). Useful to center something on screen before a screenshot.";

        public string InputSchemaJson =>
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""x"": { ""type"": ""number"" },
                    ""y"": { ""type"": ""number"" },
                    ""z"": { ""type"": ""number"" }
                },
                ""required"": [""x"", ""y"", ""z""]
            }";

        public LookAtTool(IGlobalWorldActions globalWorldActions, World world, Entity playerEntity, ExposedCameraData exposedCameraData)
        {
            this.globalWorldActions = globalWorldActions;
            this.world = world;
            this.playerEntity = playerEntity;
            this.exposedCameraData = exposedCameraData;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("x", out float x) || !arguments.TryGetFloat("y", out float y) || !arguments.TryGetFloat("z", out float z))
                return McpToolResult.Error("x, y and z world coordinates are required.");

            await UniTask.SwitchToMainThread(ct);

            Vector3 playerPosition = world.Get<CharacterTransform>(playerEntity).Position;
            globalWorldActions.RotateCamera(new Vector3(x, y, z), playerPosition);

            // Let the Cinemachine systems apply the look-at intent before reading the camera back.
            await UniTask.DelayFrame(3, cancellationToken: ct);

            var result = new JObject
            {
                ["cameraPosition"] = McpJson.Vector(exposedCameraData.WorldPosition.Value),
                ["cameraRotationEuler"] = McpJson.Vector(exposedCameraData.WorldRotation.Value.eulerAngles),
            };

            return McpToolResult.Text(result.ToString(Formatting.Indented));
        }
    }
}
