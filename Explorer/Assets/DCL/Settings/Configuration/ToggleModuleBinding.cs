using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Friends.UserBlocking;
using DCL.FeatureFlags;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Quality.Runtime;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using ECS.SceneLifeCycle.IncreasingRadius;
using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using Utility;

namespace DCL.Settings.Configuration
{
    [Serializable]
    public class ToggleModuleBinding : SettingsModuleBinding<SettingsToggleModuleView, SettingsToggleModuleView.Config, ToggleModuleBinding.ToggleFeatures>
    {
        // Values are persisted as ints in SettingsMenuConfiguration.asset.
        // Never renumber or reuse a value; new entries must pick the next unused integer.
        public enum ToggleFeatures
        {
            ChatSoundsFeature = 0,
            GraphicsVsyncToggleFeature = 1,
            HideBlockedUserChatMessagesFeature = 2,
            HeadSyncFeature = 3,
            ChatReactionsEnabledFeature = 4,
            HdrFeature = 5,
            BloomFeature = 6,
            AvatarOutlineFeature = 7,
            SunShadowsFeature = 8,
            SceneShadowsFeature = 9,
            SceneLightsFeature = 10,
            FullscreenFeature = 11,
            PlayCurrentSceneStreamOnlyFeature = 12,
            SunLensFlareFeature = 13,
            DoubleTapToMove = 14,
            MuteMicInBackgroundFeature = 15,
            SpringBoneSimulationFeature = 16,
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
                ToggleFeatures.GraphicsVsyncToggleFeature => new GraphicsVSyncController(viewInstance, qualitySettingsController),
                ToggleFeatures.HideBlockedUserChatMessagesFeature => new HideBlockedUsersChatMessagesController(viewInstance, userBlockingCache),
                ToggleFeatures.HeadSyncFeature => new HeadSyncController(viewInstance),
                ToggleFeatures.ChatReactionsEnabledFeature => CreateChatReactionsController(viewInstance, chatSettingsAsset),
                ToggleFeatures.HdrFeature => CreateSimpleToggle(viewInstance, qualitySettingsController, qualitySettingsController.SetHdr, x => x.Hdr),
                ToggleFeatures.BloomFeature => CreateSimpleToggle(viewInstance, qualitySettingsController, qualitySettingsController.SetBloom, x => x.Bloom),
                ToggleFeatures.AvatarOutlineFeature => CreateSimpleToggle(viewInstance, qualitySettingsController, qualitySettingsController.SetAvatarOutline, x => x.AvatarOutline),
                ToggleFeatures.SunShadowsFeature => CreateSimpleToggle(viewInstance, qualitySettingsController, qualitySettingsController.SetSunShadows, x => x.SunShadows),
                ToggleFeatures.SunLensFlareFeature => CreateSimpleToggle(viewInstance, qualitySettingsController, qualitySettingsController.SetSunLensFlare, x => x.SunLensFlare),
                ToggleFeatures.SceneShadowsFeature => CreateSimpleToggle(viewInstance, qualitySettingsController, qualitySettingsController.SetSceneLightShadows, x => x.SceneLightShadows),
                ToggleFeatures.SceneLightsFeature => CreateSimpleToggle(viewInstance, qualitySettingsController, qualitySettingsController.SetSceneLights, x => x.SceneLights),
                ToggleFeatures.FullscreenFeature => new FullscreenSettingsController(viewInstance),
                ToggleFeatures.PlayCurrentSceneStreamOnlyFeature => new PlayCurrentSceneStreamSettingsController(viewInstance, videoPrioritizationSettings, qualitySettingsController),
                ToggleFeatures.DoubleTapToMove => new DoubleTapToMoveSettingsController(viewInstance),
                ToggleFeatures.MuteMicInBackgroundFeature => new MuteMicInBackgroundController(viewInstance),
                ToggleFeatures.SpringBoneSimulationFeature => CreateSimpleToggle(viewInstance, qualitySettingsController, qualitySettingsController.SetSpringBoneSimulation, x => x.SpringBoneSimulation),
                // add other cases...
                _ => throw new ArgumentOutOfRangeException(nameof(viewInstance))
            };

            controller.SetView(viewInstance);
            return controller;
        }

        private static SettingsFeatureController CreateChatReactionsController(
            SettingsToggleModuleView view, ChatSettingsAsset chatSettingsAsset)
        {
            if (FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CHAT_REACTIONS_ENABLED))
                return new ChatReactionsEnabledController(view, chatSettingsAsset);

            view.gameObject.SetActive(false);

            chatSettingsAsset.SetReactionsEnabled(false);

            return new NoOpSettingsFeatureController();
        }

        private sealed class NoOpSettingsFeatureController : SettingsFeatureController
        {
            public override void Dispose() { }
        }

        private static SimpleQualitySettingFeatureController CreateSimpleToggle(
            SettingsToggleModuleView view,
            QualitySettingsController qualitySettingsController,
            UnityAction<bool> setter,
            Func<IQualitySettingsController, bool> getter)
        {
            return new SimpleQualitySettingFeatureController(qualitySettingsController,
                () =>
                {
                    view.ToggleView.Toggle.onValueChanged.AddListener(setter);
                    view.ConfigureWithoutNotify(getter(qualitySettingsController));
                },
                x => view.ConfigureWithoutNotify(getter(x)),
                () => view.ToggleView.Toggle.onValueChanged.RemoveListener(setter)
            );
        }
    }
}
