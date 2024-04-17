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
    public class SliderModuleBinding : SettingsModuleBinding<SettingsSliderModuleView, SettingsSliderModuleView.Config, SliderModuleBinding.SliderFeatures>
    {
        public enum SliderFeatures
        {
            SCENE_DISTANCE_FEATURE,
            ENVIRONMENT_DISTANCE_FEATURE,
            MOUSE_SENSITIVITY_FEATURE,
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
            AudioMixer generalAudioMixer)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            switch (Feature)
            {
                case SliderFeatures.SCENE_DISTANCE_FEATURE:
                    return new SceneDistanceSettingsController(viewInstance, realmPartitionSettingsAsset);
                case SliderFeatures.ENVIRONMENT_DISTANCE_FEATURE:
                    return new EnvironmentDistanceSettingsController(viewInstance, landscapeData);
                case SliderFeatures.MOUSE_SENSITIVITY_FEATURE:
                    return new MouseSensitivitySettingsController(viewInstance);
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
