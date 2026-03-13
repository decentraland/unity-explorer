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
using DCL.SkyBox;
using DCL.Utilities;
using ECS.Prioritization;
using ECS.SceneLifeCycle.IncreasingRadius;
using Global.AppArgs;
using System;
using UnityEngine;
using UnityEngine.Audio;
using Utility;

namespace DCL.Settings.Configuration
{
    [Serializable]
    public class ToggleModuleBinding : SettingsModuleBinding<SettingsToggleModuleView, SettingsToggleModuleView.Config, ToggleModuleBinding.ToggleFeatures>
    {
        public enum ToggleFeatures
        {
            CHAT_SOUNDS_FEATURE,
            GRAPHICS_VSYNC_TOGGLE_FEATURE,
            HIDE_BLOCKED_USER_CHAT_MESSAGES_FEATURE,
            HEAD_SYNC_FEATURE,
            HDR_FEATURE,
            BLOOM_FEATURE,
            AVATAR_OUTLINE_FEATURE,
            SUN_SHADOWS_FEATURE,
            SCENE_SHADOWS_FEATURE,
            SCENE_LIGHTS_FEATURE,
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
                ToggleFeatures.GRAPHICS_VSYNC_TOGGLE_FEATURE => new GraphicsVSyncController(viewInstance, qualitySettingsController),
                ToggleFeatures.HIDE_BLOCKED_USER_CHAT_MESSAGES_FEATURE => new HideBlockedUsersChatMessagesController(viewInstance, userBlockingCacheProxy),
                ToggleFeatures.HEAD_SYNC_FEATURE => new HeadSyncController(viewInstance),
                ToggleFeatures.HDR_FEATURE => new HdrSettingsController(viewInstance, qualitySettingsController),
                ToggleFeatures.BLOOM_FEATURE => new BloomSettingsController(viewInstance, qualitySettingsController),
                ToggleFeatures.AVATAR_OUTLINE_FEATURE => new AvatarOutlineSettingsController(viewInstance, qualitySettingsController),
                ToggleFeatures.SUN_SHADOWS_FEATURE => new SunShadowsSettingsController(viewInstance, qualitySettingsController),
                ToggleFeatures.SCENE_SHADOWS_FEATURE => new SceneShadowsSettingsController(viewInstance, qualitySettingsController),
                ToggleFeatures.SCENE_LIGHTS_FEATURE => new SceneLightsSettingsController(viewInstance, qualitySettingsController),
                // add other cases...
                _ => throw new ArgumentOutOfRangeException(nameof(viewInstance))
            };

            controller.SetView(viewInstance);
            return controller;
        }
    }
}
