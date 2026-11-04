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
    public class LobbyStagePresetModuleBinding : SettingsModuleBinding<SettingsLobbyStagePresetModuleView, SettingsLobbyStagePresetModuleView.Config, LobbyStagePresetModuleBinding.Features>
    {
        // The binding base takes a feature enum even though this view has a single controller
        public enum Features
        {
            LobbyStagePreset = 0,
        }

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
            PointAtMarkerVisibilitySettings pointAtMarkerVisibilitySettings,
            LobbyStagePresetSetting lobbyStagePresetSetting)
        {
            SettingsLobbyStagePresetModuleView viewInstance = (await assetsProvisioner.ProvideInstanceAsync(View, parent)).Value;
            viewInstance.Configure(Config);

            var controller = new LobbyStagePresetSettingsController(viewInstance, lobbyStagePresetSetting);
            controller.SetView(viewInstance);
            return controller;
        }
    }
}
