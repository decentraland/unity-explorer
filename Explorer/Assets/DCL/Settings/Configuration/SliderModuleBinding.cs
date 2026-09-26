using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Friends.UserBlocking;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using ECS.SceneLifeCycle.IncreasingRadius;
using System;
using DCL.Audio;
using DCL.Quality.Runtime;
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
            SceneDistanceFeature = 0,
            EnvironmentDistanceFeature = 1,
            MouseVerticalSensitivityFeature = 2,
            MouseHorizontalSensitivityFeature = 3,
            MasterVolumeFeature = 4,
            WorldSoundsVolumeFeature = 5,
            MusicVolumeFeature = 6,
            UiSoundsVolumeFeature = 7,
            AvatarSoundsVolumeFeature = 8,
            VoiceChatVolumeFeature = 9,
            UpscalerFeature = 10,
            MusicSFXSoundVolumeFeature = 11,
            MaxSceneLightsFeature = 12,
            ShadowDistanceFeature = 13,
        }

        public override async UniTask<SettingsFeatureController> CreateModuleAsync(
            Transform parent,
            QualitySettingsController qualitySettingsController,
            VideoPrioritizationSettings videoPrioritizationSettings,
            AudioMixer generalAudioMixer,
            ControlsSettingsAsset controlsSettingsAsset,
            ChatSettingsAsset chatSettingsAsset,
            ISystemMemoryCap systemMemoryCap,
            SceneLoadingLimit sceneLoadingLimit,
            IUserBlockingCache userBlockingCache,
            ISettingsModuleEventListener settingsEventListener,
            IAssetsProvisioner assetsProvisioner,
            VolumeBus volumeBus,
            IEventBus eventBus,
            PointAtMarkerVisibilitySettings pointAtMarkerVisibilitySettings)
        {
            var viewInstance = (await assetsProvisioner.ProvideInstanceAsync(View, parent)).Value;
            viewInstance.Configure(Config);

            SettingsFeatureController controller = Feature switch
            {
                SliderFeatures.SceneDistanceFeature => CreateSimpleSlider(viewInstance, qualitySettingsController, v => qualitySettingsController.SetSceneDistance((int)v), x => x.SceneDistance),
                SliderFeatures.EnvironmentDistanceFeature => CreateSimpleSlider(viewInstance, qualitySettingsController, qualitySettingsController.SetLandscapeDistance, x => x.LandscapeDistance),
                SliderFeatures.MouseVerticalSensitivityFeature => new MouseVerticalSensitivitySettingsController(viewInstance, controlsSettingsAsset),
                SliderFeatures.MouseHorizontalSensitivityFeature => new MouseHorizontalSensitivitySettingsController(viewInstance, controlsSettingsAsset),
                SliderFeatures.MasterVolumeFeature => new MasterVolumeSettingsController(viewInstance, generalAudioMixer, volumeBus),
                SliderFeatures.WorldSoundsVolumeFeature => new WorldSoundsVolumeSettingsController(viewInstance, generalAudioMixer, volumeBus),
                SliderFeatures.MusicSFXSoundVolumeFeature => new MusicAndSFXVolumeSettingsController(viewInstance, generalAudioMixer, volumeBus),
                SliderFeatures.MusicVolumeFeature => new MusicVolumeSettingsController(viewInstance, generalAudioMixer),
                SliderFeatures.UiSoundsVolumeFeature => new UISoundsVolumeSettingsController(viewInstance, generalAudioMixer),
                SliderFeatures.AvatarSoundsVolumeFeature => new AvatarSoundsVolumeSettingsController(viewInstance, generalAudioMixer),
                SliderFeatures.VoiceChatVolumeFeature => new VoiceChatVolumeSettingsController(viewInstance, generalAudioMixer, volumeBus),
                SliderFeatures.UpscalerFeature => new UpscalingSettingsController(viewInstance, qualitySettingsController),
                SliderFeatures.MaxSceneLightsFeature => CreateSimpleSlider(viewInstance, qualitySettingsController, v => qualitySettingsController.SetMaxSceneLights((int)v), x => x.MaxSceneLights),
                SliderFeatures.ShadowDistanceFeature => CreateSimpleSlider(viewInstance, qualitySettingsController, v => qualitySettingsController.SetShadowDistance((int)v), x => x.ShadowDistance),
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
