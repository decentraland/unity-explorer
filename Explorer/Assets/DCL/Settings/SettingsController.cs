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
        private enum SettingsSection
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
        private readonly VoiceChatSettingsAsset voiceChatSettings;
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
            VoiceChatSettingsAsset voiceChatSettings,
            VolumeBus volumeBus,
            UpscalingController upscalingController,
            bool isTranslationChatEnabled,
            IAssetsProvisioner assetsProvisioner,
            IEventBus eventBus)
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
            this.voiceChatSettings = voiceChatSettings;
            this.upscalingController = upscalingController;
            this.isTranslationChatEnabled = isTranslationChatEnabled;
            this.assetsProvisioner = assetsProvisioner;
            this.eventBus = eventBus;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();

            view.GeneralSectionButton.Button.onClick.AddListener(() => OpenSection(SettingsSection.GENERAL, settingsMenuConfiguration.GeneralSectionConfig.SettingsGroups.Count));
            view.GraphicsSectionButton.Button.onClick.AddListener(() => OpenSection(SettingsSection.GRAPHICS, settingsMenuConfiguration.GraphicsSectionConfig.SettingsGroups.Count));
            view.SoundSectionButton.Button.onClick.AddListener(() => OpenSection(SettingsSection.SOUND, settingsMenuConfiguration.SoundSectionConfig.SettingsGroups.Count));
            view.ControlsSectionButton.Button.onClick.AddListener(() => OpenSection(SettingsSection.CONTROLS, settingsMenuConfiguration.ControlsSectionConfig.SettingsGroups.Count));
            view.ChatSectionButton.Button.onClick.AddListener(() => OpenSection(SettingsSection.CHAT, settingsMenuConfiguration.ChatSectionConfig.SettingsGroups.Count));
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

            await GenerateSettingsSectionAsync(settingsMenuConfiguration.GeneralSectionConfig, view.GeneralSectionContainer);
            await GenerateSettingsSectionAsync(settingsMenuConfiguration.GraphicsSectionConfig, view.GraphicsSectionContainer);
            await GenerateSettingsSectionAsync(settingsMenuConfiguration.SoundSectionConfig, view.SoundSectionContainer);
            await GenerateSettingsSectionAsync(settingsMenuConfiguration.ControlsSectionConfig, view.ControlsSectionContainer);
            await GenerateSettingsSectionAsync(settingsMenuConfiguration.ChatSectionConfig, view.ChatSectionContainer);

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

                SettingsGroupView generalGroupView = (await assetsProvisioner.ProvideInstanceAsync(settingsMenuConfiguration.SettingsGroupPrefab, sectionContainer)).Value;

                if (!string.IsNullOrEmpty(group.GroupTitle))
                    generalGroupView.GroupTitle.text = group.GroupTitle;
                else
                    generalGroupView.GroupTitle.gameObject.SetActive(false);

                foreach (SettingsModuleBindingBase module in group.Modules)
                    if (module != null)
                        controllers.Add(await module.CreateModuleAsync
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
                            voiceChatSettings,
                            upscalingController,
                            assetsProvisioner,
                            volumeBus,
                            isTranslationChatEnabled,
                            eventBus));
            }
        }

        private void SetInitialSectionsVisibility()
        {
            view.GeneralSectionButton.gameObject.SetActive(settingsMenuConfiguration.GeneralSectionConfig.SettingsGroups.Count > 0);
            view.GraphicsSectionButton.gameObject.SetActive(settingsMenuConfiguration.GraphicsSectionConfig.SettingsGroups.Count > 0);
            view.SoundSectionButton.gameObject.SetActive(settingsMenuConfiguration.SoundSectionConfig.SettingsGroups.Count > 0);
            view.ControlsSectionButton.gameObject.SetActive(settingsMenuConfiguration.ControlsSectionConfig.SettingsGroups.Count > 0);
            view.ChatSectionButton.gameObject.SetActive(settingsMenuConfiguration.ChatSectionConfig.SettingsGroups.Count > 0);

            if (settingsMenuConfiguration.GeneralSectionConfig.SettingsGroups.Count > 0)
                OpenSection(SettingsSection.GENERAL, settingsMenuConfiguration.GeneralSectionConfig.SettingsGroups.Count);
            else if (settingsMenuConfiguration.GraphicsSectionConfig.SettingsGroups.Count > 0)
                OpenSection(SettingsSection.GRAPHICS, settingsMenuConfiguration.GraphicsSectionConfig.SettingsGroups.Count);
            else if (settingsMenuConfiguration.SoundSectionConfig.SettingsGroups.Count > 0)
                OpenSection(SettingsSection.SOUND, settingsMenuConfiguration.SoundSectionConfig.SettingsGroups.Count);
            else if (settingsMenuConfiguration.ControlsSectionConfig.SettingsGroups.Count > 0)
                OpenSection(SettingsSection.CONTROLS, settingsMenuConfiguration.ControlsSectionConfig.SettingsGroups.Count);
            else if (settingsMenuConfiguration.ChatSectionConfig.SettingsGroups.Count > 0)
                OpenSection(SettingsSection.CHAT, settingsMenuConfiguration.ChatSectionConfig.SettingsGroups.Count);
        }

        private void OpenSection(SettingsSection section, int settingsGroupCount)
        {
            bool isGeneralSection = section == SettingsSection.GENERAL;
            bool isGraphicsSection = section == SettingsSection.GRAPHICS;
            bool isSoundSection = section == SettingsSection.SOUND;
            bool isControlsSection = section == SettingsSection.CONTROLS;
            bool isChatSection = section == SettingsSection.CHAT;

            view.GeneralSectionContainer.gameObject.SetActive(isGeneralSection && settingsGroupCount > 0);
            view.GraphicsSectionContainer.gameObject.SetActive(isGraphicsSection && settingsGroupCount > 0);
            view.SoundSectionContainer.gameObject.SetActive(isSoundSection && settingsGroupCount > 0);
            view.ControlsSectionContainer.gameObject.SetActive(isControlsSection && settingsGroupCount > 0);
            view.ChatSectionContainer.gameObject.SetActive(isChatSection && settingsGroupCount > 0);

            view.GeneralSectionButton.SetSelected(isGeneralSection);
            view.GraphicsSectionButton.SetSelected(isGraphicsSection);
            view.SoundSectionButton.SetSelected(isSoundSection);
            view.ControlsSectionButton.SetSelected(isControlsSection);
            view.ChatSectionButton.SetSelected(isChatSection);

            view.BackgroundImage.sprite = section switch
              {
                    SettingsSection.GENERAL => view.GeneralSectionBackground,
                    SettingsSection.GRAPHICS => view.GraphicsSectionBackground,
                    SettingsSection.SOUND => view.SoundSectionBackground,
                    SettingsSection.CONTROLS => view.ControlsSectionBackground,
                    SettingsSection.CHAT => view.ChatSectionBackground,
                    _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
              };
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        public void Dispose()
        {
            foreach (SettingsFeatureController controller in controllers)
                controller.Dispose();

            view.GeneralSectionButton.Button.onClick.RemoveAllListeners();
            view.GraphicsSectionButton.Button.onClick.RemoveAllListeners();
            view.SoundSectionButton.Button.onClick.RemoveAllListeners();
            view.ControlsSectionButton.Button.onClick.RemoveAllListeners();
            view.ChatSectionButton.Button.onClick.RemoveAllListeners();
        }
    }
}
