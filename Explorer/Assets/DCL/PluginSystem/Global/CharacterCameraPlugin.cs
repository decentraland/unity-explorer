﻿using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Character.CharacterCamera.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
using DCL.CharacterCamera.Systems;
using DCL.DebugUtilities;
using DCL.Settings.Settings;
using ECS.Prioritization.Components;
using Global.AppArgs;
using System.Threading;
using UnityEngine;
using ApplyCinemachineSettingsSystem = DCL.Character.CharacterCamera.Systems.ApplyCinemachineSettingsSystem;
using ControlCinemachineVirtualCameraSystem = DCL.Character.CharacterCamera.Systems.ControlCinemachineVirtualCameraSystem;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Registers dependencies for the CharacterCamera feature
    /// </summary>
    public class CharacterCameraPlugin : IDCLGlobalPlugin<CharacterCameraSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ExposedCameraData exposedCameraData;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly IAppArgs commandLineArgs;
        private readonly DCLInput input;
        private readonly RealmSamplingData realmSamplingData;
        private ProvidedInstance<CinemachinePreset> providedCinemachinePreset;
        private ProvidedInstance<CinemachineCameraAudioSettings> cinemachineCameraAudioSettings;
        private ProvidedAsset<ControlsSettingsAsset> controlsSettingsAsset;

        public CharacterCameraPlugin(
            IAssetsProvisioner assetsProvisioner,
            RealmSamplingData realmSamplingData,
            ExposedCameraData exposedCameraData,
            IDebugContainerBuilder debugBuilder,
            IAppArgs commandLineArgs,
            DCLInput input)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.realmSamplingData = realmSamplingData;
            this.exposedCameraData = exposedCameraData;
            this.debugBuilder = debugBuilder;
            this.commandLineArgs = commandLineArgs;
            this.input = input;
        }

        public void Dispose()
        {
            providedCinemachinePreset.Dispose();
        }

        public async UniTask InitializeAsync(CharacterCameraSettings settings, CancellationToken ct)
        {
            providedCinemachinePreset = await assetsProvisioner.ProvideInstanceAsync(settings.cinemachinePreset, Vector3.zero, Quaternion.identity, ct: ct);
            cinemachineCameraAudioSettings = await assetsProvisioner.ProvideInstanceAsync(settings.cinemachineCameraAudioSettingsReference, Vector3.zero, Quaternion.identity, ct: ct);
            controlsSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.controlsSettingsAsset, ct: ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            Arch.Core.World world = builder.World;

            // Initialize Camera to follow the player
            PlayerComponent playerFocus = world.Get<PlayerComponent>(arguments.PlayerEntity);

            ICinemachinePreset cinemachinePreset = providedCinemachinePreset.Value;

            cinemachinePreset.FirstPersonCameraData.Camera.Follow = playerFocus.CameraFocus;
            cinemachinePreset.ThirdPersonCameraData.Camera.Follow = playerFocus.CameraFocus;
            cinemachinePreset.ThirdPersonCameraData.Camera.LookAt = playerFocus.CameraFocus;
            cinemachinePreset.DroneViewCameraData.Camera.Follow = playerFocus.CameraFocus;
            cinemachinePreset.DroneViewCameraData.Camera.LookAt = playerFocus.CameraFocus;

            cinemachinePreset.Brain.ControlledObject = cinemachinePreset.Brain.gameObject;

            // Create a special camera entity
            var cameraComponent = new CameraComponent(cinemachinePreset.Brain.OutputCamera)
            {
                PlayerFocus = playerFocus.CameraFocus,
            };

            Entity cameraEntity = world.Create(
                new CRDTEntity(SpecialEntitiesID.CAMERA_ENTITY),
                cameraComponent,
                new CursorComponent(),
                new CameraFieldOfViewComponent(),
                exposedCameraData,
                cinemachinePreset,
                new CinemachineCameraState(),
                realmSamplingData
            );

            exposedCameraData.CameraEntityProxy.SetObject(cameraEntity);

            // Register systems
            ControlCinemachineVirtualCameraSystem.InjectToWorld(ref builder, cinemachineCameraAudioSettings.Value);
            ApplyCinemachineCameraInputSystem.InjectToWorld(ref builder, input, isFreeCameraAllowed: commandLineArgs.HasDebugFlag());
            PrepareExposedCameraDataSystem.InjectToWorld(ref builder, cinemachinePreset.Brain);
            ChinemachineFieldOfViewSystem.InjectToWorld(ref builder);
            ApplyCinemachineSettingsSystem.InjectToWorld(ref builder, debugBuilder, controlsSettingsAsset.Value);
        }
    }
}
