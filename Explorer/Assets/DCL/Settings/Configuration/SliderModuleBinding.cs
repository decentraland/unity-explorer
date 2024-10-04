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
    public class SliderModuleBinding : SettingsModuleBinding<SettingsSliderModuleView, SettingsSliderModuleView.Config, SliderModuleBinding.SliderFeatures>
    {
        public enum SliderFeatures
        {
            SCENE_DISTANCE_FEATURE,
            ENVIRONMENT_DISTANCE_FEATURE,
            MOUSE_VERTICAL_SENSITIVITY_FEATURE,
            MOUSE_HORIZONTAL_SENSITIVITY_FEATURE,
            MASTER_VOLUME_FEATURE,
            WORLD_SOUNDS_VOLUME_FEATURE,
            MUSIC_VOLUME_FEATURE,
            UI_SOUNDS_VOLUME_FEATURE,
            AVATAR_SOUNDS_VOLUME_FEATURE,
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
                case SliderFeatures.SCENE_DISTANCE_FEATURE:
                    controller = new SceneDistanceSettingsController(viewInstance, realmPartitionSettingsAsset);
                    break;
                case SliderFeatures.ENVIRONMENT_DISTANCE_FEATURE:
                    controller = new EnvironmentDistanceSettingsController(viewInstance, landscapeData);
                    break;
                case SliderFeatures.MOUSE_VERTICAL_SENSITIVITY_FEATURE:
                    controller = new MouseVerticalSensitivitySettingsController(viewInstance, controlsSettingsAsset);
                    break;
                case SliderFeatures.MOUSE_HORIZONTAL_SENSITIVITY_FEATURE:
                    controller = new MouseHorizontalSensitivitySettingsController(viewInstance, controlsSettingsAsset);
                    break;
                case SliderFeatures.MASTER_VOLUME_FEATURE:
                    controller = new MasterVolumeSettingsController(viewInstance, generalAudioMixer);
                    break;
                case SliderFeatures.WORLD_SOUNDS_VOLUME_FEATURE:
                    controller = new WorldSoundsVolumeSettingsController(viewInstance, generalAudioMixer);
                    break;
                case SliderFeatures.MUSIC_VOLUME_FEATURE:
                    controller = new MusicVolumeSettingsController(viewInstance, generalAudioMixer);
                    break;
                case SliderFeatures.UI_SOUNDS_VOLUME_FEATURE:
                    controller = new UISoundsVolumeSettingsController(viewInstance, generalAudioMixer);
                    break;
                case SliderFeatures.AVATAR_SOUNDS_VOLUME_FEATURE:
                    controller = new AvatarSoundsVolumeSettingsController(viewInstance, generalAudioMixer);
                    break;
                // add other cases...
                default: throw new ArgumentOutOfRangeException(nameof(viewInstance));
            }

            controller.SetView(viewInstance);
            return controller;
        }
    }
}
