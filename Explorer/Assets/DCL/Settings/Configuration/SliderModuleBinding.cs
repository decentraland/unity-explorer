using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
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
using DCL.Audio;
using DCL.SkyBox;
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
            VOICE_CHAT_VOLUME_FEATURE,
            UPSCALER_FEATURE,
            MUSIC_SFX_SOUND_VOLUME_FEATURE,
            // add other features...
        }

        public override async UniTask<SettingsFeatureController> CreateModuleAsync(
            Transform parent,
            RealmPartitionSettingsAsset realmPartitionSettingsAsset,
            VideoPrioritizationSettings videoPrioritizationSettings,
            LandscapeData landscapeData,
            AudioMixer generalAudioMixer,
            QualitySettingsAsset qualitySettingsAsset,
            SkyboxSettingsAsset skyboxSettingsAsset,
            ControlsSettingsAsset controlsSettingsAsset,
            ChatSettingsAsset chatSettingsAsset,
            ISystemMemoryCap systemMemoryCap,
            SceneLoadingLimit sceneLoadingLimit,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            ISettingsModuleEventListener settingsEventListener,
            VoiceChatSettingsAsset voiceChatSettings,
            UpscalingController upscalingController,
            IAssetsProvisioner  assetsProvisioner,
            VolumeBus volumeBus)
        {
            var viewInstance = (await assetsProvisioner.ProvideInstanceAsync(View, parent)).Value;
            viewInstance.Configure(Config);

            SettingsFeatureController controller = Feature switch
            {
                SliderFeatures.SCENE_DISTANCE_FEATURE => new SceneDistanceSettingsController(viewInstance, realmPartitionSettingsAsset),
                SliderFeatures.ENVIRONMENT_DISTANCE_FEATURE => new EnvironmentDistanceSettingsController(viewInstance, landscapeData),
                SliderFeatures.MOUSE_VERTICAL_SENSITIVITY_FEATURE => new MouseVerticalSensitivitySettingsController(viewInstance, controlsSettingsAsset),
                SliderFeatures.MOUSE_HORIZONTAL_SENSITIVITY_FEATURE => new MouseHorizontalSensitivitySettingsController(viewInstance, controlsSettingsAsset),
                SliderFeatures.MASTER_VOLUME_FEATURE => new MasterVolumeSettingsController(viewInstance, generalAudioMixer, volumeBus),
                SliderFeatures.WORLD_SOUNDS_VOLUME_FEATURE => new WorldSoundsVolumeSettingsController(viewInstance, generalAudioMixer, volumeBus),
                SliderFeatures.MUSIC_SFX_SOUND_VOLUME_FEATURE => new MusicAndSFXVolumeSettingsController(viewInstance,generalAudioMixer, volumeBus),
                SliderFeatures.MUSIC_VOLUME_FEATURE => new MusicVolumeSettingsController(viewInstance, generalAudioMixer),
                SliderFeatures.UI_SOUNDS_VOLUME_FEATURE => new UISoundsVolumeSettingsController(viewInstance, generalAudioMixer),
                SliderFeatures.AVATAR_SOUNDS_VOLUME_FEATURE => new AvatarSoundsVolumeSettingsController(viewInstance, generalAudioMixer),
                SliderFeatures.VOICE_CHAT_VOLUME_FEATURE => new VoiceChatVolumeSettingsController(viewInstance, generalAudioMixer),
                SliderFeatures.UPSCALER_FEATURE => new UpscalingSettingsController(viewInstance, upscalingController),
                // add other cases...
                _ => throw new ArgumentOutOfRangeException(),
            };
            return controller;
        }
    }
}
