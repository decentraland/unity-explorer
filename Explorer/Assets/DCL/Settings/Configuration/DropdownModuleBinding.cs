using DCL.Landscape.Settings;
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
            // add other features...
        }

        public override SettingsFeatureController CreateModule(
            Transform parent,
            RealmPartitionSettingsAsset realmPartitionSettingsAsset,
            LandscapeData landscapeData,
            AudioMixer generalAudioMixer)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            switch (Feature)
            {
                case DropdownFeatures.GRAPHICS_QUALITY_FEATURE:
                    return new GraphicsQualitySettingsController(viewInstance);
                case DropdownFeatures.CAMERA_LOCK_FEATURE:
                    return new CameraLockSettingsController(viewInstance);
                case DropdownFeatures.CAMERA_SHOULDER_FEATURE:
                    return new CameraShoulderSettingsController(viewInstance);
                case DropdownFeatures.RESOLUTION_FEATURE:
                    return new ResolutionSettingsController(viewInstance);
                case DropdownFeatures.WINDOW_MODE_FEATURE:
                    return new WindowModeSettingsController(viewInstance);
                case DropdownFeatures.FPS_LIMIT_FEATURE:
                    return new FpsLimitSettingsController(viewInstance);
                // add other cases...
            }

            throw new ArgumentOutOfRangeException(nameof(viewInstance));
        }
    }
}
