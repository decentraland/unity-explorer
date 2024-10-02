using DCL.Landscape.Settings;
using DCL.Optimization.PerformanceBudgeting;
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
            ControlsSettingsAsset controlsSettingsAsset,
            ISystemMemoryCap systemMemoryCap)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            switch (Feature)
            {
                case SliderFeatures.SCENE_DISTANCE_FEATURE:
                    return new SceneDistanceSettingsController(viewInstance, realmPartitionSettingsAsset);
                case SliderFeatures.ENVIRONMENT_DISTANCE_FEATURE:
                    return new EnvironmentDistanceSettingsController(viewInstance, landscapeData);
                case SliderFeatures.MOUSE_VERTICAL_SENSITIVITY_FEATURE:
                    return new MouseVerticalSensitivitySettingsController(viewInstance, controlsSettingsAsset);
                case SliderFeatures.MOUSE_HORIZONTAL_SENSITIVITY_FEATURE:
                    return new MouseHorizontalSensitivitySettingsController(viewInstance, controlsSettingsAsset);
                case SliderFeatures.MASTER_VOLUME_FEATURE:
                    return new MasterVolumeSettingsController(viewInstance, generalAudioMixer);
                case SliderFeatures.WORLD_SOUNDS_VOLUME_FEATURE:
                    return new WorldSoundsVolumeSettingsController(viewInstance, generalAudioMixer);
                case SliderFeatures.MUSIC_VOLUME_FEATURE:
                    return new MusicVolumeSettingsController(viewInstance, generalAudioMixer);
                case SliderFeatures.UI_SOUNDS_VOLUME_FEATURE:
                    return new UISoundsVolumeSettingsController(viewInstance, generalAudioMixer);
                case SliderFeatures.AVATAR_SOUNDS_VOLUME_FEATURE:
                    return new AvatarSoundsVolumeSettingsController(viewInstance, generalAudioMixer);
                // add other cases...
            }

            throw new ArgumentOutOfRangeException(nameof(viewInstance));
        }
    }
}
