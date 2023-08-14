using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
using DCL.CharacterCamera.Systems;
using ECS.Prioritization.Components;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Registers dependencies for the CharacterCamera feature
    /// </summary>
    public class CharacterCameraPlugin : IDCLGlobalPlugin<CharacterCameraSettings>
    {
        private ProvidedInstance<CinemachinePreset> providedCinemachinePreset;

        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly RealmSamplingData realmSamplingData;
        private readonly CameraSamplingData cameraSamplingData;

        public CharacterCameraPlugin(IAssetsProvisioner assetsProvisioner, RealmSamplingData realmSamplingData, CameraSamplingData cameraSamplingData)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.realmSamplingData = realmSamplingData;
            this.cameraSamplingData = cameraSamplingData;
        }

        public async UniTask Initialize(CharacterCameraSettings settings, CancellationToken ct)
        {
            providedCinemachinePreset = await assetsProvisioner.ProvideInstance(settings.cinemachinePreset, Vector3.zero, Quaternion.identity, ct: ct);
        }

        public void Dispose()
        {
            providedCinemachinePreset.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            World world = builder.World;

            // Initialize Camera to follow the player
            PlayerComponent playerFocus = world.Get<PlayerComponent>(arguments.PlayerEntity);

            ICinemachinePreset cinemachinePreset = providedCinemachinePreset.Value;

            cinemachinePreset.FirstPersonCameraData.Camera.Follow = playerFocus.CameraFocus;
            cinemachinePreset.ThirdPersonCameraData.Camera.Follow = playerFocus.CameraFocus;
            cinemachinePreset.ThirdPersonCameraData.Camera.LookAt = playerFocus.CameraFocus;

            cinemachinePreset.Brain.ControlledObject = cinemachinePreset.Brain.gameObject;

            // Create a special camera entity
            world.Create(
                new CRDTEntity(SpecialEntititiesID.CAMERA_ENTITY),
                new CameraComponent(cinemachinePreset.Brain.OutputCamera),
                cinemachinePreset,
                new CinemachineCameraState(),
                cameraSamplingData,
                realmSamplingData);

            // Register systems
            ControlCinemachineVirtualCameraSystem.InjectToWorld(ref builder);
            ApplyCinemachineCameraInputSystem.InjectToWorld(ref builder);
        }
    }
}
