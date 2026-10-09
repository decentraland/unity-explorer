using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Friends.UserBlocking;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Quality.Runtime;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SingleScene;
using System;
using UnityEngine;
using UnityEngine.Audio;
using Utility;

namespace DCL.Settings.Configuration
{
    [Serializable]
    public class SpacerModuleBinding : SettingsModuleBindingBase
    {
        [field: SerializeField] public ViewRef View { get; private set; } = null!;

        public override async UniTask<SettingsFeatureController?> CreateModuleAsync(
            Transform parent,
            QualitySettingsController qualitySettingsController,
            VideoPrioritizationSettings videoPrioritizationSettings,
            AudioMixer generalAudioMixer,
            ControlsSettingsAsset controlsSettingsAsset,
            ChatSettingsAsset chatSettingsAsset,
            ISystemMemoryCap systemMemoryCap,
            SceneLoadingLimit sceneLoadingLimit,
            SingleSceneMode singleSceneMode,
            IUserBlockingCache userBlockingCache,
            ISettingsModuleEventListener settingsEventListener,
            IAssetsProvisioner assetsProvisioner,
            VolumeBus volumeBus,
            IEventBus eventBus,
            PointAtMarkerVisibilitySettings pointAtMarkerVisibilitySettings)
        {
            // Nothing to drive: the instance exists only to take up a grid cell
            await assetsProvisioner.ProvideInstanceAsync(View, parent);
            return null;
        }

        [Serializable]
        public class ViewRef : ComponentReference<SettingsSpacerModuleView>
        {
            public ViewRef(string guid) : base(guid) { }
        }
    }
}
