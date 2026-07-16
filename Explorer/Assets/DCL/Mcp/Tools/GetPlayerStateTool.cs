using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Mcp.Protocol;
using DCL.Profiles;
using ECS.SceneLifeCycle.CurrentScene;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Mcp.Tools
{
    public class GetPlayerStateTool : IMcpTool
    {
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly ExposedCameraData exposedCameraData;
        private readonly ICurrentSceneInfo currentSceneInfo;

        public string Name => "get_player_state";

        public string Description =>
            "Read the player's current world position, rotation, parcel, velocity and grounded state, the camera position, rotation and mode, "
            + "and the wallet address — use the address to tell Explorer instances apart when several run at once.";

        public string InputSchemaJson => @"{ ""type"": ""object"", ""properties"": {} }";

        public GetPlayerStateTool(World world, Entity playerEntity, ExposedCameraData exposedCameraData, ICurrentSceneInfo currentSceneInfo)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.exposedCameraData = exposedCameraData;
            this.currentSceneInfo = currentSceneInfo;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            CharacterTransform characterTransform = world.Get<CharacterTransform>(playerEntity);
            Vector3 position = characterTransform.Position;

            world.TryGet(playerEntity, out CharacterRigidTransform? rigidTransform);
            world.TryGet(playerEntity, out Profile? profile);

            var state = new JObject
            {
                ["position"] = McpJson.Vector(position),
                ["rotationEuler"] = McpJson.Vector(characterTransform.Rotation.eulerAngles),
                ["parcel"] = McpJson.Parcel(position.ToParcel()),
                ["velocity"] = McpJson.Vector(rigidTransform?.MoveVelocity.Velocity ?? Vector3.zero),
                ["isGrounded"] = rigidTransform?.IsGrounded ?? false,
                ["isPlayerStandingOnScene"] = currentSceneInfo.IsPlayerStandingOnScene,
                ["address"] = profile == null ? JValue.CreateNull() : profile.Compact.UserId,
                ["camera"] = new JObject
                {
                    ["position"] = McpJson.Vector(exposedCameraData.WorldPosition.Value),
                    ["rotationEuler"] = McpJson.Vector(exposedCameraData.WorldRotation.Value.eulerAngles),
                    ["mode"] = exposedCameraData.CameraMode.ToString(),
                    ["modeChangeAllowed"] = SetCameraModeTool.IsModeChangeAllowed(world),
                    ["pointerLocked"] = exposedCameraData.PointerIsLocked.Value,
                },
            };

            return McpToolResult.Text(state.ToString(Formatting.Indented));
        }
    }
}
