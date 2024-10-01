using DCL.Landscape.Settings;
using DCL.Quality;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
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
            // add other features...
        }

        public override SettingsFeatureController CreateModule(
            Transform parent,
            RealmPartitionSettingsAsset realmPartitionSettingsAsset,
            LandscapeData landscapeData,
            AudioMixer generalAudioMixer,
            QualitySettingsAsset qualitySettingsAsset)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            return Feature switch
                   {
                       DropdownFeatures.GRAPHICS_QUALITY_FEATURE => new GraphicsQualitySettingsController(viewInstance, realmPartitionSettingsAsset, landscapeData, qualitySettingsAsset),
                       DropdownFeatures.CAMERA_LOCK_FEATURE => new CameraLockSettingsController(viewInstance),
                       DropdownFeatures.CAMERA_SHOULDER_FEATURE => new CameraShoulderSettingsController(viewInstance),
                       DropdownFeatures.RESOLUTION_FEATURE => new ResolutionSettingsController(viewInstance),
                       DropdownFeatures.WINDOW_MODE_FEATURE => new WindowModeSettingsController(viewInstance),
                       DropdownFeatures.FPS_LIMIT_FEATURE => new FpsLimitSettingsController(viewInstance),
                       DropdownFeatures.MEMORY_LIMIT_FEATURE => new MemoryLimitSettingController(viewInstance),
                       _ => throw new ArgumentOutOfRangeException(nameof(viewInstance))
                   };
        }
    }
}
