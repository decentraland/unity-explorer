using DCL.Landscape.Settings;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Quality;
using DCL.SDKComponents.MediaStream.Settings;
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
            VideoPrioritizationSettings videoPrioritizationSettings,
            LandscapeData landscapeData,
            AudioMixer generalAudioMixer,
            QualitySettingsAsset qualitySettingsAsset,
            ControlsSettingsAsset controlsSettingsAsset,
            ISystemMemoryCap systemMemoryCap,
            WorldVolumeMacBus worldVolumeMacBus = null)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            SettingsFeatureController controller = Feature switch
                                                   {
                                                       SliderFeatures.SCENE_DISTANCE_FEATURE => new SceneDistanceSettingsController(viewInstance, realmPartitionSettingsAsset),
                                                       SliderFeatures.ENVIRONMENT_DISTANCE_FEATURE => new EnvironmentDistanceSettingsController(viewInstance, landscapeData),
                                                       SliderFeatures.MOUSE_VERTICAL_SENSITIVITY_FEATURE => new MouseVerticalSensitivitySettingsController(viewInstance, controlsSettingsAsset),
                                                       SliderFeatures.MOUSE_HORIZONTAL_SENSITIVITY_FEATURE => new MouseHorizontalSensitivitySettingsController(viewInstance, controlsSettingsAsset),
                                                       SliderFeatures.MASTER_VOLUME_FEATURE => new MasterVolumeSettingsController(viewInstance, generalAudioMixer, worldVolumeMacBus),
                                                       SliderFeatures.WORLD_SOUNDS_VOLUME_FEATURE => new WorldSoundsVolumeSettingsController(viewInstance, generalAudioMixer, worldVolumeMacBus),
                                                       SliderFeatures.MUSIC_VOLUME_FEATURE => new MusicVolumeSettingsController(viewInstance, generalAudioMixer),
                                                       SliderFeatures.UI_SOUNDS_VOLUME_FEATURE => new UISoundsVolumeSettingsController(viewInstance, generalAudioMixer),
                                                       SliderFeatures.AVATAR_SOUNDS_VOLUME_FEATURE => new AvatarSoundsVolumeSettingsController(viewInstance, generalAudioMixer),
                                                       // add other cases...
                                                       _ => throw new ArgumentOutOfRangeException(nameof(viewInstance))
                                                   };

            controller.SetView(viewInstance);
            return controller;
        }
    }
}
