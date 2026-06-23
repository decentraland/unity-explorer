using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.FeatureFlags;
using DCL.Friends.UserBlocking;
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
using Utility;

namespace DCL.Settings.Configuration
{
    /// <summary>
    /// We need this class to serialize by ref
    /// </summary>
    [Serializable]
    public abstract class SettingsModuleBindingBase
    {
        [field: SerializeField]
        public FeatureId FeatureId { get; set; } = FeatureId.NONE;

        public abstract UniTask<SettingsFeatureController?> CreateModuleAsync(
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
            PointAtMarkerVisibilitySettings pointAtMarkerVisibilitySettings);
    }

    [Serializable]
    public abstract class SettingsModuleBinding<TView, TConfig, TControllerType> : SettingsModuleBindingBase
        where TView : SettingsModuleView<TConfig>
        where TConfig : SettingsModuleViewConfiguration
        where TControllerType : Enum
    {
        [field: SerializeField] public ViewRef View { get; private set; }
        [field: SerializeField] public TConfig Config { get; private set; }
        [field: SerializeField] public TControllerType Feature { get; private set; }

        [Serializable]
        public class ViewRef : ComponentReference<TView>
        {
            public ViewRef(string guid) : base(guid) { }
        }
    }
}
