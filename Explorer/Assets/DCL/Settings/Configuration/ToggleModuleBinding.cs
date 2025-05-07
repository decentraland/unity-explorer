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
using System;
using UnityEngine;
using UnityEngine.Audio;

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
            ChatAudioSettingsAsset chatAudioSettingsAsset,
            ISystemMemoryCap systemMemoryCap,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            WorldVolumeMacBus worldVolumeMacBus = null)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            SettingsFeatureController controller = Feature switch
                                                   {
                                                       ToggleFeatures.GRAPHICS_VSYNC_TOGGLE_FEATURE => new GraphicsVSyncController(viewInstance),
                                                       ToggleFeatures.HIDE_BLOCKED_USER_CHAT_MESSAGES_FEATURE => new HideBlockedUsersChatMessagesController(viewInstance, userBlockingCacheProxy),
                                                       // add other cases...
                                                       _ => throw new ArgumentOutOfRangeException(nameof(viewInstance))
                                                   };

            controller.SetView(viewInstance);
            return controller;
        }
    }
}
