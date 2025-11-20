using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Friends.UserBlocking;
using DCL.Landscape.Settings;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Quality;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.Configuration;
using DCL.Settings.ModuleControllers;
using DCL.Settings.Settings;
using DCL.SkyBox;
using DCL.UI;
using DCL.Utilities;
using ECS.Prioritization;
using ECS.SceneLifeCycle.IncreasingRadius;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Settings
{
    public class SettingsController : ISection, IDisposable, ISettingsModuleEventListener
    {
        public enum SettingsSection
        {
            GENERAL,
            GRAPHICS,
            SOUND,
            CONTROLS,
            CHAT
        }

        private readonly SettingsView view;
        private readonly SettingsMenuConfiguration settingsMenuConfiguration;
        private readonly AudioMixer generalAudioMixer;
        private readonly RealmPartitionSettingsAsset realmPartitionSettingsAsset;
        private readonly VideoPrioritizationSettings videoPrioritizationSettings;
        private readonly LandscapeData landscapeData;
        private readonly QualitySettingsAsset qualitySettingsAsset;
        private readonly SkyboxSettingsAsset skyboxSettingsAsset;
        private readonly ISystemMemoryCap memoryCap;
        private readonly SceneLoadingLimit sceneLoadingLimit;
        private readonly VolumeBus volumeBus;
        private readonly ControlsSettingsAsset controlsSettingsAsset;
        private readonly RectTransform rectTransform;
        private readonly List<SettingsFeatureController> controllers = new ();
        private readonly ChatSettingsAsset chatSettingsAsset;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly UpscalingController upscalingController;
        private readonly bool isTranslationChatEnabled;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IEventBus eventBus;
        private readonly IAppArgs appParameters;

        private readonly IReadOnlyDictionary<SettingsSection, (Transform container, ButtonWithSelectableStateView button, Sprite background, SettingsSectionConfig config)> sections;

        public event Action<ChatBubbleVisibilitySettings> ChatBubblesVisibilityChanged;

        public SettingsController(
            SettingsView view,
            SettingsMenuConfiguration settingsMenuConfiguration,
            AudioMixer generalAudioMixer,
            RealmPartitionSettingsAsset realmPartitionSettingsAsset,
            VideoPrioritizationSettings videoPrioritizationSettings,
            LandscapeData landscapeData,
            QualitySettingsAsset qualitySettingsAsset,
            SkyboxSettingsAsset skyboxSettingsAsset,
            ControlsSettingsAsset controlsSettingsAsset,
            ISystemMemoryCap memoryCap,
            ChatSettingsAsset chatSettingsAsset,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            SceneLoadingLimit sceneLoadingLimit,
            VolumeBus volumeBus,
            UpscalingController upscalingController,
            bool isTranslationChatEnabled,
            IAssetsProvisioner assetsProvisioner,
            IEventBus eventBus,
            IAppArgs appParameters)
        {
            this.view = view;
            this.settingsMenuConfiguration = settingsMenuConfiguration;
            this.generalAudioMixer = generalAudioMixer;
            this.realmPartitionSettingsAsset = realmPartitionSettingsAsset;
            this.landscapeData = landscapeData;
            this.qualitySettingsAsset = qualitySettingsAsset;
            this.skyboxSettingsAsset = skyboxSettingsAsset;
            this.memoryCap = memoryCap;
            this.chatSettingsAsset = chatSettingsAsset;
            this.volumeBus = volumeBus;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.controlsSettingsAsset = controlsSettingsAsset;
            this.videoPrioritizationSettings = videoPrioritizationSettings;
            this.sceneLoadingLimit = sceneLoadingLimit;
            this.upscalingController = upscalingController;
            this.isTranslationChatEnabled = isTranslationChatEnabled;
            this.assetsProvisioner = assetsProvisioner;
            this.eventBus = eventBus;
            this.appParameters = appParameters;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();

            sections = new Dictionary<SettingsSection, (Transform container, ButtonWithSelectableStateView button, Sprite background, SettingsSectionConfig config)>
            {
                [SettingsSection.GENERAL] = (view.GeneralSectionContainer, view.GeneralSectionButton, view.GeneralSectionBackground, settingsMenuConfiguration.GeneralSectionConfig),
                [SettingsSection.GRAPHICS] = (view.GraphicsSectionContainer, view.GraphicsSectionButton, view.GraphicsSectionBackground, settingsMenuConfiguration.GraphicsSectionConfig),
                [SettingsSection.SOUND] = (view.SoundSectionContainer, view.SoundSectionButton, view.SoundSectionBackground, settingsMenuConfiguration.SoundSectionConfig),
                [SettingsSection.CONTROLS] = (view.ControlsSectionContainer, view.ControlsSectionButton, view.ControlsSectionBackground, settingsMenuConfiguration.ControlsSectionConfig),
                [SettingsSection.CHAT] = (view.ChatSectionContainer, view.ChatSectionButton, view.ChatSectionBackground, settingsMenuConfiguration.ChatSectionConfig),
            };

            foreach (var pair in sections)
                pair.Value.button!.Button.onClick!.AddListener(() => OpenSection(pair.Key, pair.Value.config!.SettingsGroups.Count));
        }

        public UniTask InitializeAsync() =>
            GenerateSettingsAsync();

        public void Activate()
        {
            view.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
        }

        public void Toggle(SettingsSection section)
        {
            var config = sections[section];
            OpenSection(section, config.config!.SettingsGroups.Count);
        }

        public void Animate(int triggerId)
        {
            view.PanelAnimator.SetTrigger(triggerId);
            view.HeaderAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            view.PanelAnimator.Rebind();
            view.HeaderAnimator.Rebind();
            view.PanelAnimator.Update(0);
            view.HeaderAnimator.Update(0);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings newVisibility)
        {
            ChatBubblesVisibilityChanged?.Invoke(newVisibility);
        }

        private async UniTask GenerateSettingsAsync()
        {
            if (settingsMenuConfiguration.SettingsGroupPrefab == null)
            {
                ReportHub.LogError(ReportCategory.SETTINGS_MENU, $"Settings Group prefab not found! Please set it the SettingsMenuConfiguration asset.");
                return;
            }

            foreach (var pair in sections)
                await GenerateSettingsSectionAsync(pair.Value.config!, pair.Value.container!);

            foreach (var controller in controllers)
                controller.OnAllControllersInstantiated(controllers);

            SetInitialSectionsVisibility();
        }

        private async UniTask GenerateSettingsSectionAsync(SettingsSectionConfig sectionConfig, Transform sectionContainer)
        {
            foreach (SettingsGroup group in sectionConfig.SettingsGroups)
            {
                if (group.FeatureFlagName != FeatureFlag.None && !FeatureFlagsConfiguration.Instance.IsEnabled(group.FeatureFlagName.GetStringValue()))
                    return;

                if (group.FeatureId != FeatureId.NONE && !FeaturesRegistry.Instance.IsEnabled(group.FeatureId)) return;

                SettingsGroupView generalGroupView = (await assetsProvisioner.ProvideInstanceAsync(settingsMenuConfiguration.SettingsGroupPrefab, sectionContainer)).Value;

                if (!string.IsNullOrEmpty(group.GroupTitle))
                    generalGroupView.GroupTitle.text = group.GroupTitle;
                else
                    generalGroupView.GroupTitle.gameObject.SetActive(false);

                foreach (SettingsModuleBindingBase module in group.Modules)
                    if (module != null)
                    {
                        if (module.FeatureId != FeatureId.NONE && !FeaturesRegistry.Instance.IsEnabled(module.FeatureId))
                            continue;

                        var controller =
                            await module.CreateModuleAsync
                            (
                                generalGroupView.ModulesContainer,
                                realmPartitionSettingsAsset,
                                videoPrioritizationSettings,
                                landscapeData,
                                generalAudioMixer,
                                qualitySettingsAsset,
                                skyboxSettingsAsset,
                                controlsSettingsAsset,
                                chatSettingsAsset,
                                memoryCap,
                                sceneLoadingLimit,
                                userBlockingCacheProxy,
                                this,
                                upscalingController,
                                assetsProvisioner,
                                volumeBus,
                                isTranslationChatEnabled,
                                eventBus,
                                appParameters);

                        if (controller != null)
                            controllers.Add(controller);
                    }
            }
        }

        private void SetInitialSectionsVisibility()
        {
            foreach (var pair in sections)
                pair.Value.button!.gameObject.SetActive(pair.Value.config!.SettingsGroups.Count > 0);

            foreach (var pair in sections)
                if (pair.Value.config!.SettingsGroups.Count > 0)
                {
                    OpenSection(pair.Key, pair.Value.config.SettingsGroups.Count);
                    break;
                }
        }

        private void OpenSection(SettingsSection section, int settingsGroupCount)
        {
            foreach ((SettingsSection current, (Transform container, ButtonWithSelectableStateView button, Sprite _, SettingsSectionConfig _)) in sections)
            {
                bool opened = section == current;
                container.gameObject.SetActive(opened && settingsGroupCount > 0);
                button.SetSelected(opened);
            }

            view.BackgroundImage.sprite = sections[section].background!;
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        public void Dispose()
        {
            foreach (SettingsFeatureController controller in controllers)
                controller.Dispose();

            foreach (var pair in sections)
                pair.Value.button!.Button.onClick!.RemoveAllListeners();
        }
    }
}
