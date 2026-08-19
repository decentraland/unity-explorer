using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.InWorldCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using ECS.Abstract;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Switches the camera mode by writing <see cref="CameraComponent.Mode" /> (the same pattern scene systems use;
    ///     <c>ControlCinemachineVirtualCameraSystem</c> applies it next frame). The direct write bypasses the user-input
    ///     gates, so this tool re-checks them itself and refuses when a scene holds the camera.
    /// </summary>
    public class SetCameraModeTool : McpTool
    {
        private static readonly CameraMode[] ALLOWED_MODES = { CameraMode.FirstPerson, CameraMode.ThirdPerson, CameraMode.DroneView, CameraMode.Free };

        private readonly World world;
        private readonly ExposedCameraData exposedCameraData;

        public override string Name => "set_camera_mode";

        public override string Description =>
            "Switch the player camera mode (first_person, third_person, drone_view, or the free-fly camera), like a user pressing the camera key. "
            + "Refuses with an explanation when the scene locks the mode (CameraModeArea, scene virtual camera, photo camera). "
            + "Any player movement drops free back to third_person.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Enum("mode", "Target camera mode.", ALLOWED_MODES, isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public SetCameraModeTool(World world, ExposedCameraData exposedCameraData)
        {
            this.world = world;
            this.exposedCameraData = exposedCameraData;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetEnum("mode", out CameraMode targetMode, ALLOWED_MODES))
                return McpToolResult.Error("mode must be one of: first_person, third_person, drone_view, free.");

            string? blockReason = TrySwitchMode(world, targetMode, out CameraMode previousMode);

            if (blockReason != null)
                return McpToolResult.Error(blockReason);

            // Let ControlCinemachineVirtualCameraSystem activate the matching virtual camera before reading back.
            await UniTask.DelayFrame(2, cancellationToken: ct);

            var result = new JObject
            {
                ["requestedMode"] = McpWireEnum<CameraMode>.ToWire(targetMode),
                ["currentMode"] = McpWireEnum<CameraMode>.ToWire(exposedCameraData.CameraMode),
                ["previousMode"] = McpWireEnum<CameraMode>.ToWire(previousMode),
            };

            return McpToolResult.Json(result);
        }

        /// <summary>
        ///     Same gates <c>ControlCinemachineVirtualCameraSystem.HandleCameraInput</c> applies to user input.
        ///     Main thread only.
        /// </summary>
        internal static bool IsModeChangeAllowed(World world)
        {
            SingleInstanceEntity cameraEntity = world.CacheCamera();
            ref readonly CameraComponent camera = ref cameraEntity.GetCameraComponent(world);

            return camera.Mode != CameraMode.SdkCamera
                   && camera.CameraInputChangeEnabled
                   && !world.Has<InWorldCameraComponent>(cameraEntity);
        }

        internal static string? TrySwitchMode(World world, CameraMode targetMode, out CameraMode previousMode)
        {
            SingleInstanceEntity cameraEntity = world.CacheCamera();
            ref CameraComponent camera = ref cameraEntity.GetCameraComponent(world);
            previousMode = camera.Mode;

            if (camera.Mode == CameraMode.SdkCamera)
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
