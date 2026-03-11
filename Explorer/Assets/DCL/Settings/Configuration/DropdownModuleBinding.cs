using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Friends.UserBlocking;
using DCL.Landscape.Settings;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Quality;
using DCL.Quality.Runtime;
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
using Utility;
using DCL.SkyBox;
using Global.AppArgs;

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
            MEMORY_LIMIT_FEATURE,
            CHAT_NEARBY_AUDIO_MODES_FEATURE,
            CHAT_DMS_AUDIO_MODES_FEATURE,
            CHAT_DMS_MODES_FEATURE,
            CHAT_BUBBLES_MODES_FEATURE,
            VOICECHAT_INPUT_DEVICE,
            CHAT_TRANSLATE_FEATURE,
            MSAA_FEATURE,
            SHADOWS_QUALITY_FEATURE,
            // add other features...
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
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            ISettingsModuleEventListener settingsEventListener,
            UpscalingController upscalingController,
            IAssetsProvisioner assetsProvisioner,
            VolumeBus volumeBus,
            bool isTranslationChatEnabled,
            IEventBus eventBus,
            IAppArgs appParameters)
        {
            var viewInstance = (await assetsProvisioner.ProvideInstanceAsync(View, parent)).Value;
            viewInstance.Configure(Config);

            SettingsFeatureController controller = Feature switch
            {
                // DropdownFeatures.GRAPHICS_QUALITY_FEATURE => new GraphicsPresetSettingsController_OLD(viewInstance,
                //     realmPartitionSettingsAsset,
                //     landscapeData,
                //     qualitySettingsAsset,
                //     skyboxSettingsAsset),

                DropdownFeatures.GRAPHICS_QUALITY_FEATURE => new GraphicsPresetSettingsController(viewInstance, qualitySettingsController),
                DropdownFeatures.CAMERA_LOCK_FEATURE => new CameraLockSettingsController(viewInstance),
                DropdownFeatures.CAMERA_SHOULDER_FEATURE => new CameraShoulderSettingsController(viewInstance),
                DropdownFeatures.RESOLUTION_FEATURE => new ResolutionSettingsController(viewInstance, qualitySettingsController),
                DropdownFeatures.WINDOW_MODE_FEATURE => new WindowModeSettingsController(viewInstance, qualitySettingsController),
                DropdownFeatures.FPS_LIMIT_FEATURE => new FpsLimitSettingsController(viewInstance, qualitySettingsController),

                DropdownFeatures.MEMORY_LIMIT_FEATURE => new MemoryLimitSettingController(viewInstance,
                    systemMemoryCap,
                    sceneLoadingLimit),

                DropdownFeatures.CHAT_NEARBY_AUDIO_MODES_FEATURE => new ChatSoundsSettingsController(viewInstance,
                    generalAudioMixer,
                    chatSettingsAsset),

                DropdownFeatures.CHAT_DMS_MODES_FEATURE => new ChatPrivacySettingsController(viewInstance,
                    chatSettingsAsset),

                DropdownFeatures.CHAT_BUBBLES_MODES_FEATURE => new ChatBubblesVisibilityController(viewInstance,
                    chatSettingsAsset,
                    settingsEventListener),

                DropdownFeatures.VOICECHAT_INPUT_DEVICE => new InputDeviceController(viewInstance),

                DropdownFeatures.CHAT_TRANSLATE_FEATURE => new ChatTranslationSettingsController(viewInstance,
                    chatSettingsAsset,
                    isTranslationChatEnabled,
                    eventBus),
                DropdownFeatures.MSAA_FEATURE => new MSAASettingsController(viewInstance, qualitySettingsController),
                DropdownFeatures.SHADOWS_QUALITY_FEATURE => new ShadowsQualitySettingsController(viewInstance, qualitySettingsController),
                // add other cases...
                _ => throw new ArgumentOutOfRangeException(nameof(viewInstance))
            };

            controller.SetView(viewInstance);
            return controller;
        }
    }
}
