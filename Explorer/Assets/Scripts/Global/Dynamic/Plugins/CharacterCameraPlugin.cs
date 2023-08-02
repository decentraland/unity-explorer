using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using ECS.Prioritization.Components;

namespace Global.Dynamic.Plugins
{
    /// <summary>
    ///     Registers dependencies for the CharacterCamera feature
    /// </summary>
    public class CharacterCameraPlugin : IECSGlobalPlugin
    {
        private readonly ICinemachinePreset cinemachinePreset;
        private readonly RealmSamplingData realmSamplingData;
        private readonly CameraSamplingData cameraSamplingData;

        internal CharacterCameraPlugin(ICinemachinePreset cinemachinePreset,
            RealmSamplingData realmSamplingData,
            CameraSamplingData cameraSamplingData)
        {
            this.cinemachinePreset = cinemachinePreset;
            this.realmSamplingData = realmSamplingData;
            this.cameraSamplingData = cameraSamplingData;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            World world = builder.World;

            // Initialize Camera to follow the player
            PlayerComponent playerFocus = world.Get<PlayerComponent>(arguments.PlayerEntity);

            cinemachinePreset.FirstPersonCameraData.Camera.Follow = playerFocus.CameraFocus;
            cinemachinePreset.ThirdPersonCameraData.Camera.Follow = playerFocus.CameraFocus;
            cinemachinePreset.ThirdPersonCameraData.Camera.LookAt = playerFocus.CameraFocus;

            // Create a special camera entity
            world.Create(
                new CRDTEntity(SpecialEntititiesID.CAMERA_ENTITY),
                new CameraComponent(cinemachinePreset.Brain.OutputCamera),
                cinemachinePreset,
                new CinemachineCameraState(),
                new CameraInputSettings(cinemachinePreset.CameraModeMouseWheelThreshold),
                cameraSamplingData,
                realmSamplingData);
        }
    }
}
