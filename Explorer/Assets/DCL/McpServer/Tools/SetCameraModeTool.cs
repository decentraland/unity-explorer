using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.InWorldCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using ECS.Abstract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Switches the camera mode by writing <see cref="CameraComponent.Mode" /> (the same pattern scene systems use;
    ///     <c>ControlCinemachineVirtualCameraSystem</c> applies it next frame). The direct write bypasses the user-input
    ///     gates, so this tool re-checks them itself and refuses when a scene holds the camera.
    /// </summary>
    public class SetCameraModeTool : IMcpTool
    {
        private readonly World world;
        private readonly ExposedCameraData exposedCameraData;

        public string Name => "set_camera_mode";

        public string Description =>
            "Switch the player camera mode (first_person, third_person, drone, or the free-fly camera), like a user pressing the camera key. "
            + "Refuses with an explanation when the scene locks the mode (CameraModeArea, scene virtual camera, photo camera). "
            + "Any player movement drops free back to third_person.";

        public JObject InputSchema =>
            McpInputSchema.Object()
                          .String("mode", "Target camera mode.", enumValues: new[] { "first_person", "third_person", "drone", "free" }, required: true)
                          .Build();

        public McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public SetCameraModeTool(World world, ExposedCameraData exposedCameraData)
        {
            this.world = world;
            this.exposedCameraData = exposedCameraData;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            CameraMode targetMode;

            switch (arguments.GetString("mode", string.Empty))
            {
                case "first_person": targetMode = CameraMode.FirstPerson; break;
                case "third_person": targetMode = CameraMode.ThirdPerson; break;
                case "drone": targetMode = CameraMode.DroneView; break;
                case "free": targetMode = CameraMode.Free; break;
                default: return McpToolResult.Error("mode must be one of: first_person, third_person, drone, free.");
            }

            await UniTask.SwitchToMainThread(ct);

            string? blockReason = TrySwitchMode(world, targetMode, out CameraMode previousMode);

            if (blockReason != null)
                return McpToolResult.Error(blockReason);

            // Let ControlCinemachineVirtualCameraSystem activate the matching virtual camera before reading back.
            await UniTask.DelayFrame(2, cancellationToken: ct);

            var result = new JObject
            {
                ["requestedMode"] = targetMode.ToString(),
                ["currentMode"] = exposedCameraData.CameraMode.ToString(),
                ["previousMode"] = previousMode.ToString(),
            };

            return McpToolResult.Text(result.ToString(Formatting.Indented));
        }

        /// <summary>
        ///     Same gates <c>ControlCinemachineVirtualCameraSystem.HandleCameraInput</c> applies to user input.
        ///     Main thread only.
        /// </summary>
        internal static bool IsModeChangeAllowed(World world)
        {
            SingleInstanceEntity cameraEntity = world.CacheCamera();
            ref readonly CameraComponent camera = ref cameraEntity.GetCameraComponent(world);

            return camera.Mode != CameraMode.SDKCamera
                   && camera.CameraInputChangeEnabled
                   && !world.Has<InWorldCameraComponent>(cameraEntity);
        }

        internal static string? TrySwitchMode(World world, CameraMode targetMode, out CameraMode previousMode)
        {
            SingleInstanceEntity cameraEntity = world.CacheCamera();
            ref CameraComponent camera = ref cameraEntity.GetCameraComponent(world);
            previousMode = camera.Mode;

            if (camera.Mode == CameraMode.SDKCamera)
                return "A scene virtual camera controls the view right now (mode SDKCamera); the mode cannot change until the scene releases it.";

            if (!camera.CameraInputChangeEnabled)
                return $"Camera mode is locked by the scene (CameraModeArea; current mode: {camera.Mode}). Leave the area to change modes.";

            if (world.Has<InWorldCameraComponent>(cameraEntity))
                return "The in-world photo camera is active; close it before changing the camera mode.";

            camera.Mode = targetMode;
            return null;
        }
    }
}
