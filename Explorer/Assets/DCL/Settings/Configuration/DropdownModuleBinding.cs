using DCL.Landscape.Settings;
using DCL.Quality;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
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
            AudioMixer generalAudioMixer,
            QualitySettingsAsset qualitySettingsAsset,
            ControlsSettingsAsset controlsSettingsAsset)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            SettingsFeatureController controller;
            switch (Feature)
            {
                case DropdownFeatures.GRAPHICS_QUALITY_FEATURE:
                    controller = new GraphicsQualitySettingsController(viewInstance, realmPartitionSettingsAsset, landscapeData, qualitySettingsAsset);
                    break;
                case DropdownFeatures.CAMERA_LOCK_FEATURE:
                    controller = new CameraLockSettingsController(viewInstance);
                    break;
                case DropdownFeatures.CAMERA_SHOULDER_FEATURE:
                    controller = new CameraShoulderSettingsController(viewInstance);
                    break;
                case DropdownFeatures.RESOLUTION_FEATURE:
                    controller = new ResolutionSettingsController(viewInstance);
                    break;
                case DropdownFeatures.WINDOW_MODE_FEATURE:
                    controller = new WindowModeSettingsController(viewInstance);
                    break;
                case DropdownFeatures.FPS_LIMIT_FEATURE:
                    controller = new FpsLimitSettingsController(viewInstance);
                    break;
                // add other cases...
                default: throw new ArgumentOutOfRangeException(nameof(viewInstance));
            }

            controller.SetView(viewInstance);
            return controller;
        }
    }
}
