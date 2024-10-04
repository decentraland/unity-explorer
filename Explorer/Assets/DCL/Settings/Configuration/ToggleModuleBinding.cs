using DCL.Landscape.Settings;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Quality;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
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
            // add other features...
        }

        public override SettingsFeatureController CreateModule(
            Transform parent,
            RealmPartitionSettingsAsset realmPartitionSettingsAsset,
            LandscapeData landscapeData,
            AudioMixer generalAudioMixer,
            QualitySettingsAsset qualitySettingsAsset,
            ControlsSettingsAsset controlsSettingsAsset,
            ISystemMemoryCap systemMemoryCap)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            SettingsFeatureController controller;
            switch (Feature)
            {
                case ToggleFeatures.CHAT_SOUNDS_FEATURE:
                    controller = new ChatSoundsSettingsController(viewInstance, generalAudioMixer);
                    break;
                case ToggleFeatures.GRAPHICS_VSYNC_TOGGLE_FEATURE:
                    controller = new GraphicsVSyncController(viewInstance);
                    break;
                // add other cases...
                default: throw new ArgumentOutOfRangeException(nameof(viewInstance));
            }

            controller.SetView(viewInstance);
            return controller;
        }
    }
}
