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
            ChatSettingsAsset chatSettingsAsset,
            ISystemMemoryCap systemMemoryCap,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            WorldVolumeMacBus worldVolumeMacBus = null)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            SettingsFeatureController controller = Feature switch
                                                   {
                                                       DropdownFeatures.GRAPHICS_QUALITY_FEATURE => new GraphicsQualitySettingsController(viewInstance, realmPartitionSettingsAsset, landscapeData, qualitySettingsAsset),
                                                       DropdownFeatures.CAMERA_LOCK_FEATURE => new CameraLockSettingsController(viewInstance),
                                                       DropdownFeatures.CAMERA_SHOULDER_FEATURE => new CameraShoulderSettingsController(viewInstance),
                                                       DropdownFeatures.RESOLUTION_FEATURE => new ResolutionSettingsController(viewInstance),
                                                       DropdownFeatures.WINDOW_MODE_FEATURE => new WindowModeSettingsController(viewInstance),
                                                       DropdownFeatures.FPS_LIMIT_FEATURE => new FpsLimitSettingsController(viewInstance),
                                                       DropdownFeatures.MEMORY_LIMIT_FEATURE => new MemoryLimitSettingController(viewInstance, systemMemoryCap),
                                                       DropdownFeatures.CHAT_NEARBY_AUDIO_MODES_FEATURE => new ChatSoundsSettingsController(viewInstance, generalAudioMixer,chatSettingsAsset),
                                                       DropdownFeatures.CHAT_DMS_MODES_FEATURE => new ChatPrivacySettingsController(viewInstance, chatSettingsAsset),
                                                       DropdownFeatures.CHAT_BUBBLES_MODES_FEATURE => new ChatBubblesVisibilityController(viewInstance, chatSettingsAsset),
                                                       // add other cases...
                                                       _ => throw new ArgumentOutOfRangeException(nameof(viewInstance))
                                                   };

            controller.SetView(viewInstance);
            return controller;
        }
    }
}
