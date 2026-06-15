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
using ECS.Prioritization;
using ECS.SceneLifeCycle.IncreasingRadius;
using System;
using DCL.Audio;
using DCL.Quality.Runtime;
using DCL.SkyBox;
using Global.AppArgs;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using Utility;

namespace DCL.Settings.Configuration
{
    [Serializable]
    public class SliderModuleBinding : SettingsModuleBinding<SettingsSliderModuleView, SettingsSliderModuleView.Config, SliderModuleBinding.SliderFeatures>
    {
        // Values are persisted as ints in SettingsMenuConfiguration.asset.
        // Never renumber or reuse a value; new entries must pick the next unused integer.
        public enum SliderFeatures
        {
            SCENE_DISTANCE_FEATURE = 0,
            ENVIRONMENT_DISTANCE_FEATURE = 1,
            MOUSE_VERTICAL_SENSITIVITY_FEATURE = 2,
            MOUSE_HORIZONTAL_SENSITIVITY_FEATURE = 3,
            MASTER_VOLUME_FEATURE = 4,
            WORLD_SOUNDS_VOLUME_FEATURE = 5,
            MUSIC_VOLUME_FEATURE = 6,
            UI_SOUNDS_VOLUME_FEATURE = 7,
            AVATAR_SOUNDS_VOLUME_FEATURE = 8,
            VOICE_CHAT_VOLUME_FEATURE = 9,
            UPSCALER_FEATURE = 10,
            MUSIC_SFX_SOUND_VOLUME_FEATURE = 11,
            MAX_SCENE_LIGHTS_FEATURE = 12,
            SHADOW_DISTANCE_FEATURE = 13,
        }

        public override async UniTask<SettingsFeatureController> CreateModuleAsync(
            Transform parent,
            QualitySettingsController qualitySettingsController,
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
            IUserBlockingCache userBlockingCache,
            ISettingsModuleEventListener settingsEventListener,
            UpscalingController upscalingController,
            IAssetsProvisioner  assetsProvisioner,
            VolumeBus volumeBus,
            IEventBus eventBus,
            IAppArgs appParameters,
            PointAtMarkerVisibilitySettings pointAtMarkerVisibilitySettings)
        {
            var viewInstance = (await assetsProvisioner.ProvideInstanceAsync(View, parent)).Value;
            viewInstance.Configure(Config);

            SettingsFeatureController controller = Feature switch
            {
                SliderFeatures.SCENE_DISTANCE_FEATURE => CreateSimpleSlider(viewInstance, qualitySettingsController, v => qualitySettingsController.SetSceneDistance((int)v), x => x.SceneDistance),
                SliderFeatures.ENVIRONMENT_DISTANCE_FEATURE => CreateSimpleSlider(viewInstance, qualitySettingsController, qualitySettingsController.SetLandscapeDistance, x => x.LandscapeDistance),
                SliderFeatures.MOUSE_VERTICAL_SENSITIVITY_FEATURE => new MouseVerticalSensitivitySettingsController(viewInstance, controlsSettingsAsset),
                SliderFeatures.MOUSE_HORIZONTAL_SENSITIVITY_FEATURE => new MouseHorizontalSensitivitySettingsController(viewInstance, controlsSettingsAsset),
                SliderFeatures.MASTER_VOLUME_FEATURE => new MasterVolumeSettingsController(viewInstance, generalAudioMixer, volumeBus),
                SliderFeatures.WORLD_SOUNDS_VOLUME_FEATURE => new WorldSoundsVolumeSettingsController(viewInstance, generalAudioMixer, volumeBus),
                SliderFeatures.MUSIC_SFX_SOUND_VOLUME_FEATURE => new MusicAndSFXVolumeSettingsController(viewInstance, generalAudioMixer, volumeBus),
                SliderFeatures.MUSIC_VOLUME_FEATURE => new MusicVolumeSettingsController(viewInstance, generalAudioMixer),
                SliderFeatures.UI_SOUNDS_VOLUME_FEATURE => new UISoundsVolumeSettingsController(viewInstance, generalAudioMixer),
                SliderFeatures.AVATAR_SOUNDS_VOLUME_FEATURE => new AvatarSoundsVolumeSettingsController(viewInstance, generalAudioMixer),
                SliderFeatures.VOICE_CHAT_VOLUME_FEATURE => new VoiceChatVolumeSettingsController(viewInstance, generalAudioMixer, volumeBus),
                SliderFeatures.UPSCALER_FEATURE => new UpscalingSettingsController(viewInstance, qualitySettingsController),
                SliderFeatures.MAX_SCENE_LIGHTS_FEATURE => CreateSimpleSlider(viewInstance, qualitySettingsController, v => qualitySettingsController.SetMaxSceneLights((int)v), x => x.MaxSceneLights),
                SliderFeatures.SHADOW_DISTANCE_FEATURE => CreateSimpleSlider(viewInstance, qualitySettingsController, v => qualitySettingsController.SetShadowDistance((int)v), x => x.ShadowDistance),
                // add other cases...
                _ => throw new ArgumentOutOfRangeException(),
            };
            return controller;
        }

        private static SimpleQualitySettingFeatureController CreateSimpleSlider(
            SettingsSliderModuleView view,
            QualitySettingsController qualitySettingsController,
            UnityAction<float> setter,
            Func<IQualitySettingsController, float> getter)
        {
            return new SimpleQualitySettingFeatureController(qualitySettingsController,
                () =>
                {
                    view.SliderView.Slider.onValueChanged.AddListener(setter);
                    view.ConfigureWithoutNotify(getter(qualitySettingsController));
                },
                x => view.ConfigureWithoutNotify(getter(x)),
                () => view.SliderView.Slider.onValueChanged.RemoveAllListeners()
            );
        }
    }
}
