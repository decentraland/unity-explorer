using DCL.Friends.UserBlocking;
using DCL.Landscape.Settings;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Quality;
using DCL.Rendering.GPUInstancing;
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

namespace DCL.Settings.Configuration
{
    /// <summary>
    /// We need this class to serialize by ref
    /// </summary>
    [Serializable]
    public abstract class SettingsModuleBindingBase
    {
        public abstract SettingsFeatureController CreateModule(Transform parent,
            RealmPartitionSettingsAsset realmPartitionSettingsAsset,
            VideoPrioritizationSettings videoPrioritizationSettings,
            LandscapeData landscapeData,
            GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings roadsSettings,
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
            bool isVoiceChatEnabled);
    }

    [Serializable]
    public abstract class SettingsModuleBinding<TView, TConfig, TControllerType> : SettingsModuleBindingBase
        where TView : SettingsModuleView<TConfig>
        where TConfig : SettingsModuleViewConfiguration
        where TControllerType : Enum
    {
        [field: SerializeField]
        public TView View { get; private set; }

        [field: SerializeField]
        public TConfig Config { get; private set; }

        [field: SerializeField]
        public TControllerType Feature { get; private set; }
    }
}
