using DCL.Landscape.Settings;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Quality;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using DCL.StylizedSkybox.Scripts.Plugin;
using ECS.Prioritization;
using System;
using UnityEngine;
using UnityEngine.Audio;

namespace DCL.Settings.Configuration
{
    [Serializable]
    public class DropdownModuleBinding : SettingsModuleBinding<SettingsDropdownModuleView, SettingsDropdownModuleView.Config, DropdownModuleBinding.DropdownFeatures>
    {
        public enum DropdownFeatures
        {
            GRAPHICS_QUALITY_FEATURE,
            CAMERA_LOCK_FEATURE,
            CAMERA_SHOULDER_FEATURE,
            RESOLUTION_FEATURE,
            WINDOW_MODE_FEATURE,
            FPS_LIMIT_FEATURE,
            MEMORY_LIMIT_FEATURE,
            SKYBOX_SPEED_FEATURE,

            // add other features...
        }

        public override SettingsFeatureController CreateModule(
            Transform parent,
            RealmPartitionSettingsAsset realmPartitionSettingsAsset,
            VideoPrioritizationSettings videoPrioritizationSettings,
            LandscapeData landscapeData,
            AudioMixer generalAudioMixer,
            QualitySettingsAsset qualitySettingsAsset,
            ControlsSettingsAsset controlsSettingsAsset,
            ISystemMemoryCap systemMemoryCap,
            StylizedSkyboxSettingsAsset skyboxSettingsAsset,
            WorldVolumeMacBus worldVolumeMacBus = null)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            SettingsFeatureController controller =
                Feature switch
                {
                    DropdownFeatures.GRAPHICS_QUALITY_FEATURE => new GraphicsQualitySettingsController(viewInstance, realmPartitionSettingsAsset, landscapeData, qualitySettingsAsset),
                    DropdownFeatures.CAMERA_LOCK_FEATURE => new CameraLockSettingsController(viewInstance),
                    DropdownFeatures.CAMERA_SHOULDER_FEATURE => new CameraShoulderSettingsController(viewInstance),
                    DropdownFeatures.RESOLUTION_FEATURE => new ResolutionSettingsController(viewInstance),
                    DropdownFeatures.WINDOW_MODE_FEATURE => new WindowModeSettingsController(viewInstance),
                    DropdownFeatures.FPS_LIMIT_FEATURE => new FpsLimitSettingsController(viewInstance),
                    DropdownFeatures.MEMORY_LIMIT_FEATURE => new MemoryLimitSettingController(viewInstance, systemMemoryCap),
                    DropdownFeatures.SKYBOX_SPEED_FEATURE => new SkyboxSpeedSettingsController(viewInstance, skyboxSettingsAsset),

                    // add other cases...
                    _ => throw new ArgumentOutOfRangeException(nameof(viewInstance))
                };

            controller.SetView(viewInstance);
            return controller;
        }
    }
}
