using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Mcp.Server;
using ECS.Abstract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;
using Utility.Arch;

namespace DCL.Mcp.Tools
{
    /// <summary>
    ///     Places the free camera at an absolute world position, optionally aiming it and setting its FOV.
    ///     Enters Free mode when needed (same scene-lock gates as <see cref="SetCameraModeTool" />) and waits
    ///     for the Cinemachine blend to reach the target before reporting the actual pose.
    /// </summary>
    public class SetCameraPoseTool : IMcpTool
    {
        private const float DEFAULT_TIMEOUT_SEC = 5f;
        private const float MIN_TIMEOUT_SEC = 0.5f;
        private const float MAX_TIMEOUT_SEC = 15f;
        private const float SETTLE_EPSILON = 0.1f;
        private const int POLL_INTERVAL_MS = 100;
        private const float MIN_FOV = 10f;
        private const float MAX_FOV = 120f;

        private readonly World world;
        private readonly Entity playerEntity;
        private readonly ExposedCameraData exposedCameraData;

        public string Name => "set_camera_pose";

        public string Description =>
            "Place the free camera at an absolute world position, optionally aiming it at a point and setting its field of view. "
            + "Enters the free camera mode if needed (refuses with the reason when the scene locks the camera). The camera stays "
            + "put while the player moves; restore a player-following view with set_camera_mode third_person.";

        public string InputSchemaJson =>
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""x"": { ""type"": ""number"", ""description"": ""Camera world position."" },
                    ""y"": { ""type"": ""number"" },
                    ""z"": { ""type"": ""number"" },
                    ""lookAtX"": { ""type"": ""number"", ""description"": ""Optional world point to aim at (all three lookAt components required together)."" },
                    ""lookAtY"": { ""type"": ""number"" },
                    ""lookAtZ"": { ""type"": ""number"" },
                    ""fov"": { ""type"": ""number"", ""description"": ""Optional vertical field of view in degrees (10-120)."" },
                    ""timeoutSec"": { ""type"": ""number"", ""description"": ""Seconds to wait for the camera to settle at the target. Default 5, max 15."" }
                },
                ""required"": [""x"", ""y"", ""z""]
            }";

        public SetCameraPoseTool(World world, Entity playerEntity, ExposedCameraData exposedCameraData)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.exposedCameraData = exposedCameraData;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("x", out float x) || !arguments.TryGetFloat("y", out float y) || !arguments.TryGetFloat("z", out float z))
                return McpToolResult.Error("x, y and z world coordinates for the camera position are required.");

            bool hasLookAtX = arguments.TryGetFloat("lookAtX", out float lookAtX);
            bool hasLookAtY = arguments.TryGetFloat("lookAtY", out float lookAtY);
            bool hasLookAtZ = arguments.TryGetFloat("lookAtZ", out float lookAtZ);
            bool hasLookAt = hasLookAtX && hasLookAtY && hasLookAtZ;

            if ((hasLookAtX || hasLookAtY || hasLookAtZ) && !hasLookAt)
                return McpToolResult.Error("lookAtX, lookAtY and lookAtZ must be provided together.");

            float? fov = null;

            if (arguments.TryGetFloat("fov", out float fovValue))
                fov = Mathf.Clamp(fovValue, MIN_FOV, MAX_FOV);

            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);
            var targetPosition = new Vector3(x, y, z);

            await UniTask.SwitchToMainThread(ct);

            SingleInstanceEntity cameraEntity = world.CacheCamera();

            if (cameraEntity.GetCameraComponent(world).Mode != CameraMode.Free)
            {
                string? blockReason = SetCameraModeTool.TrySwitchMode(world, CameraMode.Free, out CameraMode _);

                if (blockReason != null)
                    return McpToolResult.Error(blockReason);

                // Let ControlCinemachineVirtualCameraSystem activate the free vcam (and apply its default
                // spawn position, which the pose below overrides).
                await UniTask.DelayFrame(2, cancellationToken: ct);
            }

            if (!world.TryGet(cameraEntity, out ICinemachinePreset? cinemachinePreset) || cinemachinePreset == null)
                return McpToolResult.Error("The camera rig is not initialized yet.");

            cinemachinePreset.ForceFreeCameraPose(targetPosition, fov);

            if (hasLookAt)
            {
                Vector3 playerPosition = world.Get<CharacterTransform>(playerEntity).Position;
                world.AddOrSet(cameraEntity, new CameraLookAtIntent(new Vector3(lookAtX, lookAtY, lookAtZ), playerPosition));
            }

            // Entering Free blends the output camera toward the vcam over a couple of seconds; when the
            // mode was already Free the pose applies instantly and the first poll succeeds.
            var settled = false;
            float deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSec;

            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                if (Vector3.Distance(exposedCameraData.WorldPosition.Value, targetPosition) <= SETTLE_EPSILON)
                {
                    settled = true;
                    break;
                }

                await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
            }

            // Let the look-at intent apply before reading the rotation back.
            await UniTask.DelayFrame(2, cancellationToken: ct);

            var result = new JObject
            {
                ["position"] = McpJson.Vector(exposedCameraData.WorldPosition.Value),
                ["rotationEuler"] = McpJson.Vector(exposedCameraData.WorldRotation.Value.eulerAngles),
                ["mode"] = exposedCameraData.CameraMode.ToString(),
                ["settled"] = settled,
            };

            return McpToolResult.Text(result.ToString(Formatting.Indented));
        }
    }
}
