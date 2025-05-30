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
    public class TextModuleBinding : SettingsModuleBinding<SettingsTextModuleView, SettingsTextModuleView.Config, TextModuleBinding.TextFeatures>
    {
        public enum TextFeatures
        {
            VOICECHAT_CONNECTION_STRING,
            // add other text features...
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
            WorldVolumeMacBus worldVolumeMacBus = null)
        {
            var viewInstance = Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            SettingsFeatureController controller = Feature switch
                                                   {
                                                       TextFeatures.VOICECHAT_CONNECTION_STRING => new ConnectionStringController(viewInstance, voiceChatSettings),
                                                       // add other cases...
                                                       _ => throw new ArgumentOutOfRangeException(nameof(Feature))
                                                   };

            controller.SetView(viewInstance);
            return controller;
        }
    }
} 