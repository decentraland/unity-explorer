using DCL.Friends.UserBlocking;
using DCL.Landscape.Settings;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Quality;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using DCL.Utilities;
using ECS.Prioritization;
using ECS.SceneLifeCycle.IncreasingRadius;
using System;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

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
            VOICE_CHAT_VOLUME_FEATURE,
            UPSCALER_FEATURE,
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
            ChatSettingsAsset chatSettingsAsset,
            ISystemMemoryCap systemMemoryCap,
            SceneLoadingLimit sceneLoadingLimit,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            ISettingsModuleEventListener settingsEventListener,
            VoiceChatSettingsAsset voiceChatSettings,
            UpscalingController upscalingController,
            WorldVolumeMacBus worldVolumeMacBus,
            bool isVoiceChatEnabled)
        {
            var viewInstance = Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            SettingsFeatureController controller = Feature switch
                                                   {
                                                       SliderFeatures.SCENE_DISTANCE_FEATURE => new SceneDistanceSettingsController(viewInstance, realmPartitionSettingsAsset),
                                                       SliderFeatures.ENVIRONMENT_DISTANCE_FEATURE => new EnvironmentDistanceSettingsController(viewInstance, landscapeData),
                                                       SliderFeatures.MOUSE_VERTICAL_SENSITIVITY_FEATURE => new MouseVerticalSensitivitySettingsController(viewInstance, controlsSettingsAsset),
                                                       SliderFeatures.MOUSE_HORIZONTAL_SENSITIVITY_FEATURE => new MouseHorizontalSensitivitySettingsController(viewInstance, controlsSettingsAsset),
                                                       SliderFeatures.MASTER_VOLUME_FEATURE => new MasterVolumeSettingsController(viewInstance, generalAudioMixer, worldVolumeMacBus),
                                                       SliderFeatures.WORLD_SOUNDS_VOLUME_FEATURE => new WorldSoundsVolumeSettingsController(viewInstance, generalAudioMixer, worldVolumeMacBus),
                                                       SliderFeatures.UI_SOUNDS_VOLUME_FEATURE => new UISoundsVolumeSettingsController(viewInstance, generalAudioMixer),
                                                       SliderFeatures.AVATAR_SOUNDS_VOLUME_FEATURE => new AvatarSoundsVolumeSettingsController(viewInstance, generalAudioMixer),
                                                       SliderFeatures.VOICE_CHAT_VOLUME_FEATURE => new VoiceChatVolumeSettingsController(viewInstance, generalAudioMixer, isVoiceChatEnabled),
                                                       SliderFeatures.UPSCALER_FEATURE => new UpscalingSettingsController(viewInstance, upscalingController),
                                                       // add other cases...
                                                       _ => throw new ArgumentOutOfRangeException(),
                                                   };

            return controller;
        }
    }
}
