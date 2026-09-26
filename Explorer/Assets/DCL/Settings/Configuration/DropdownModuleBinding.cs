using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Friends.UserBlocking;
using DCL.FeatureFlags;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using ECS.SceneLifeCycle.IncreasingRadius;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using Utility;

namespace DCL.Settings.Configuration
{
    [Serializable]
    public class DropdownModuleBinding : SettingsModuleBinding<SettingsDropdownModuleView, SettingsDropdownModuleView.Config, DropdownModuleBinding.DropdownFeatures>
    {
        private static readonly MsaaLevel[] MSAA_LEVELS = { MsaaLevel.Off, MsaaLevel.X2, MsaaLevel.X4, MsaaLevel.X8 };
        private static readonly ShadowQualityLevel[] SHADOW_QUALITY_LEVELS = { ShadowQualityLevel.Low, ShadowQualityLevel.Medium, ShadowQualityLevel.High };

        // Values are persisted as ints in SettingsMenuConfiguration.asset.
        // Never renumber or reuse a value; new entries must pick the next unused integer.
        public enum DropdownFeatures
        {
            GraphicsQualityFeature = 0,
            CameraLockFeature = 1,
            CameraShoulderFeature = 2,
            ResolutionFeature = 3,
            WindowModeFeature = 4,
            FpsLimitFeature = 5,
            MemoryLimitFeature = 6,
            ChatNearbyAudioModesFeature = 7,
            ChatDmsAudioModesFeature = 8,
            ChatDmsModesFeature = 9,
            ChatBubblesModesFeature = 10,
            VoicechatInputDevice = 11,
            ChatTranslateFeature = 12,
            MsaaFeature = 13,
            ShadowsQualityFeature = 14,
            PointAtMarkerFeature = 15,
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
                DropdownFeatures.GraphicsQualityFeature => new GraphicsPresetSettingsController(viewInstance, qualitySettingsController),
                DropdownFeatures.CameraLockFeature => new CameraLockSettingsController(viewInstance),
                DropdownFeatures.CameraShoulderFeature => new CameraShoulderSettingsController(viewInstance),
                DropdownFeatures.ResolutionFeature => new ResolutionSettingsController(viewInstance),
                DropdownFeatures.FpsLimitFeature => new FpsLimitSettingsController(viewInstance, qualitySettingsController),

                DropdownFeatures.MemoryLimitFeature => new MemoryLimitSettingController(viewInstance,
                    systemMemoryCap,
                    sceneLoadingLimit),

                DropdownFeatures.ChatNearbyAudioModesFeature => new ChatSoundsSettingsController(viewInstance,
                    generalAudioMixer,
                    chatSettingsAsset),

                DropdownFeatures.ChatDmsModesFeature => new ChatPrivacySettingsController(viewInstance,
                    chatSettingsAsset),

                DropdownFeatures.ChatBubblesModesFeature => CreateChatBubblesController(viewInstance, chatSettingsAsset, settingsEventListener),

                DropdownFeatures.VoicechatInputDevice => new InputDeviceController(viewInstance),

                DropdownFeatures.ChatTranslateFeature => new ChatTranslationSettingsController(viewInstance,
                    chatSettingsAsset,
                    eventBus),
                DropdownFeatures.MsaaFeature => CreateDropdownQualityController(viewInstance, qualitySettingsController, MSAA_LEVELS, qualitySettingsController.SetMsaa, x => x.Msaa),
                DropdownFeatures.ShadowsQualityFeature => CreateDropdownQualityController(viewInstance, qualitySettingsController, SHADOW_QUALITY_LEVELS, qualitySettingsController.SetShadowQuality, x => x.SceneShadowQuality),

                DropdownFeatures.PointAtMarkerFeature => new PointAtMarkerVisibilityController(viewInstance, pointAtMarkerVisibilitySettings),
                // add other cases...
                _ => throw new ArgumentOutOfRangeException(nameof(viewInstance))
            };

            controller.SetView(viewInstance);
            return controller;
        }

        private static SettingsFeatureController CreateChatBubblesController(
            SettingsDropdownModuleView view,
            ChatSettingsAsset chatSettingsAsset,
            ISettingsModuleEventListener settingsEventListener)
        {
            if (!FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CHAT_REACTIONS_ENABLED))
                view.ModuleTitle.text = "In-World Chat Bubbles";

            return new ChatBubblesVisibilityController(view, chatSettingsAsset, settingsEventListener);
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
