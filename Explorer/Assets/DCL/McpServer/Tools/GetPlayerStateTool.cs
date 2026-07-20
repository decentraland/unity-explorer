using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.Profiles;
using ECS.SceneLifeCycle.CurrentScene;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.McpServer.Tools
{
    public class GetPlayerStateTool : McpTool
    {
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly ExposedCameraData exposedCameraData;
        private readonly ICurrentSceneInfo currentSceneInfo;

        public override string Name => "get_player_state";

        public override string Description =>
            "Read the player's current world position, rotation, parcel, velocity and grounded state, the camera position, rotation and mode, "
            + "and the wallet address — use the address to tell Explorer instances apart when several run at once.";

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Object("position", JObjectExtensions.VectorSchema())
                          .Object("rotationEuler", JObjectExtensions.VectorSchema())
                          .Object("parcel", JObjectExtensions.ParcelSchema())
                          .Object("velocity", JObjectExtensions.VectorSchema())
                          .Boolean("isGrounded")
                          .Boolean("isPlayerStandingOnScene")
                          .String("address", "Wallet address of the logged-in player, or null when no profile is loaded.", nullable: true)
                          .Object("camera", McpJsonSchema.Object()
                                                          .Object("position", JObjectExtensions.VectorSchema())
                                                          .Object("rotationEuler", JObjectExtensions.VectorSchema())
                                                          .String("mode")
                                                          .Boolean("modeChangeAllowed")
                                                          .Boolean("pointerLocked"))
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetPlayerStateTool(World world, Entity playerEntity, ExposedCameraData exposedCameraData, ICurrentSceneInfo currentSceneInfo)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.exposedCameraData = exposedCameraData;
            this.currentSceneInfo = currentSceneInfo;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            CharacterTransform characterTransform = world.Get<CharacterTransform>(playerEntity);
            Vector3 position = characterTransform.Position;

            world.TryGet(playerEntity, out CharacterRigidTransform? rigidTransform);
            world.TryGet(playerEntity, out Profile? profile);

            var state = new JObject
            {
                ["position"] = position.ToVector(),
                ["rotationEuler"] = characterTransform.Rotation.eulerAngles.ToVector(),
                ["parcel"] = position.ToParcel().ToParcel(),
                ["velocity"] = (rigidTransform?.MoveVelocity.Velocity ?? Vector3.zero).ToVector(),
                ["isGrounded"] = rigidTransform?.IsGrounded ?? false,
                ["isPlayerStandingOnScene"] = currentSceneInfo.IsPlayerStandingOnScene,
                ["address"] = profile == null ? JValue.CreateNull() : profile.Compact.UserId,
                ["camera"] = new JObject
                {
                    ["position"] = exposedCameraData.WorldPosition.Value.ToVector(),
                    ["rotationEuler"] = exposedCameraData.WorldRotation.Value.eulerAngles.ToVector(),
                    ["mode"] = exposedCameraData.CameraMode.ToString(),
                    ["modeChangeAllowed"] = SetCameraModeTool.IsModeChangeAllowed(world),
                    ["pointerLocked"] = exposedCameraData.PointerIsLocked.Value,
                },
            };

            return UniTask.FromResult(McpToolResult.JsonWithStructured(state));
        }
    }
}
