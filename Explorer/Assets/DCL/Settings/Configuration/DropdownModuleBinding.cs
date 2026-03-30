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
using DCL.Utilities;
using ECS.Prioritization;
using ECS.SceneLifeCycle.IncreasingRadius;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using Utility;
using DCL.SkyBox;
using Global.AppArgs;

namespace DCL.Settings.Configuration
{
    [Serializable]
    public class DropdownModuleBinding : SettingsModuleBinding<SettingsDropdownModuleView, SettingsDropdownModuleView.Config, DropdownModuleBinding.DropdownFeatures>
    {
        private static readonly MsaaLevel[] MSAA_LEVELS = { MsaaLevel.Off, MsaaLevel.X2, MsaaLevel.X4, MsaaLevel.X8 };
        private static readonly ShadowQualityLevel[] SHADOW_QUALITY_LEVELS = { ShadowQualityLevel.Low, ShadowQualityLevel.Medium, ShadowQualityLevel.High };

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
            VOICECHAT_INPUT_DEVICE,
            CHAT_TRANSLATE_FEATURE,
            MSAA_FEATURE,
            SHADOWS_QUALITY_FEATURE,
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
            IEventBus eventBus,
            IAppArgs appParameters)
        {
            var viewInstance = (await assetsProvisioner.ProvideInstanceAsync(View, parent)).Value;
            viewInstance.Configure(Config);

            SettingsFeatureController controller = Feature switch
            {
                DropdownFeatures.GRAPHICS_QUALITY_FEATURE => new GraphicsPresetSettingsController(viewInstance, qualitySettingsController),
                DropdownFeatures.CAMERA_LOCK_FEATURE => new CameraLockSettingsController(viewInstance),
                DropdownFeatures.CAMERA_SHOULDER_FEATURE => new CameraShoulderSettingsController(viewInstance),
                DropdownFeatures.RESOLUTION_FEATURE => new ResolutionSettingsController(viewInstance),
                DropdownFeatures.FPS_LIMIT_FEATURE => new FpsLimitSettingsController(viewInstance, qualitySettingsController),

                DropdownFeatures.MEMORY_LIMIT_FEATURE => new MemoryLimitSettingController(viewInstance,
                    systemMemoryCap,
                    sceneLoadingLimit),

                DropdownFeatures.CHAT_NEARBY_AUDIO_MODES_FEATURE => new ChatSoundsSettingsController(viewInstance,
                    generalAudioMixer,
                    chatSettingsAsset),

                DropdownFeatures.CHAT_DMS_MODES_FEATURE => new ChatPrivacySettingsController(viewInstance,
                    chatSettingsAsset),

                DropdownFeatures.CHAT_BUBBLES_MODES_FEATURE => new ChatBubblesVisibilityController(viewInstance,
                    chatSettingsAsset,
                    settingsEventListener),

                DropdownFeatures.VOICECHAT_INPUT_DEVICE => new InputDeviceController(viewInstance),

                DropdownFeatures.CHAT_TRANSLATE_FEATURE => new ChatTranslationSettingsController(viewInstance,
                    chatSettingsAsset,
                    eventBus),
                DropdownFeatures.MSAA_FEATURE => CreateDropdownQualityController(viewInstance, qualitySettingsController, MSAA_LEVELS, qualitySettingsController.SetMsaa, x => x.Msaa),
                DropdownFeatures.SHADOWS_QUALITY_FEATURE => CreateDropdownQualityController(viewInstance, qualitySettingsController, SHADOW_QUALITY_LEVELS, qualitySettingsController.SetShadowQuality, x => x.SceneShadowQuality),
                // add other cases...
                _ => throw new ArgumentOutOfRangeException(nameof(viewInstance))
            };

            controller.SetView(viewInstance);
            return controller;
        }

        /// <summary>
        /// Wires a dropdown view to a quality setting backed by an enum (e.g. MsaaLevel, ShadowQualityLevel).
        /// Populates options from <paramref name="levels"/>, syncs selection on preset changes, and cleans up listeners on dispose.
        /// </summary>
        private static SimpleQualitySettingFeatureController CreateDropdownQualityController<TEnum>(
            SettingsDropdownModuleView view,
            IQualitySettingsController qualitySettingsController,
            IReadOnlyList<TEnum> levels,
            Action<TEnum> setter,
            Func<IQualitySettingsController, TEnum> getter) where TEnum : Enum
        {
            return new SimpleQualitySettingFeatureController(
                qualitySettingsController,
                // Initialize: populate dropdown options and bind selection changes to the setter
                () =>
                {
                    view.DropdownView.Dropdown.ClearOptions();
                    var options = new List<TMP_Dropdown.OptionData>(levels.Count);
                    for (int i = 0; i < levels.Count; i++)
                        options.Add(new TMP_Dropdown.OptionData(levels[i].ToString()));
                    view.DropdownView.Dropdown.AddOptions(options);
                    view.DropdownView.Dropdown.onValueChanged.AddListener(index =>
                    {
                        if (index >= 0 && index < levels.Count)
                            setter(levels[index]);
                    });
                    view.DropdownView.Dropdown.SetValueWithoutNotify(IndexOf(levels, getter(qualitySettingsController)));
                },
                // OnPresetChanged: sync the dropdown selection to the current quality value
                x => view.DropdownView.Dropdown.SetValueWithoutNotify(IndexOf(levels, getter(x))),
                // Dispose: remove listeners to prevent stale references
                () => view.DropdownView.Dropdown.onValueChanged.RemoveAllListeners()
            );

            static int IndexOf<T>(IReadOnlyList<T> list, T value)
            {
                for (int i = 0; i < list.Count; i++)
                    if (EqualityComparer<T>.Default.Equals(list[i], value))
                        return i;
                return 0;
            }
        }
    }
}
