using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.Landscape.Settings;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Quality;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.Configuration;
using DCL.Settings.ModuleControllers;
using DCL.Settings.Settings;
using DCL.UI;
using DCL.Utilities;
using ECS.Prioritization;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace DCL.Settings
{
    public class SettingsController : ISection, IDisposable
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
        private readonly ISystemMemoryCap memoryCap;
        private readonly WorldVolumeMacBus worldVolumeMacBus;
        private readonly ControlsSettingsAsset controlsSettingsAsset;
        private readonly RectTransform rectTransform;
        private readonly List<SettingsFeatureController> controllers = new ();
        private readonly ChatAudioSettingsAsset chatAudioSettingsAsset;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;

        public SettingsController(
            SettingsView view,
            SettingsMenuConfiguration settingsMenuConfiguration,
            AudioMixer generalAudioMixer,
            RealmPartitionSettingsAsset realmPartitionSettingsAsset,
            VideoPrioritizationSettings videoPrioritizationSettings,
            LandscapeData landscapeData,
            QualitySettingsAsset qualitySettingsAsset,
            ControlsSettingsAsset controlsSettingsAsset,
            ISystemMemoryCap memoryCap,
            ChatAudioSettingsAsset chatAudioSettingsAsset,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            WorldVolumeMacBus worldVolumeMacBus = null)
        {
            this.view = view;
            this.settingsMenuConfiguration = settingsMenuConfiguration;
            this.generalAudioMixer = generalAudioMixer;
            this.realmPartitionSettingsAsset = realmPartitionSettingsAsset;
            this.landscapeData = landscapeData;
            this.qualitySettingsAsset = qualitySettingsAsset;
            this.memoryCap = memoryCap;
            this.chatAudioSettingsAsset = chatAudioSettingsAsset;
            this.worldVolumeMacBus = worldVolumeMacBus;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.controlsSettingsAsset = controlsSettingsAsset;
            this.videoPrioritizationSettings = videoPrioritizationSettings;

            rectTransform = view.transform.parent.GetComponent<RectTransform>();

            GenerateSettings();

            view.GeneralSectionButton.Button.onClick.AddListener(() => OpenSection(SettingsSection.GENERAL, settingsMenuConfiguration.GeneralSectionConfig.SettingsGroups.Count));
            view.GraphicsSectionButton.Button.onClick.AddListener(() => OpenSection(SettingsSection.GRAPHICS, settingsMenuConfiguration.GraphicsSectionConfig.SettingsGroups.Count));
            view.SoundSectionButton.Button.onClick.AddListener(() => OpenSection(SettingsSection.SOUND, settingsMenuConfiguration.SoundSectionConfig.SettingsGroups.Count));
            view.ControlsSectionButton.Button.onClick.AddListener(() => OpenSection(SettingsSection.CONTROLS, settingsMenuConfiguration.ControlsSectionConfig.SettingsGroups.Count));
            view.ChatSectionButton.Button.onClick.AddListener(() => OpenSection(SettingsSection.CHAT, settingsMenuConfiguration.ChatSectionConfig.SettingsGroups.Count));
        }

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

        private void GenerateSettings()
        {
            if (settingsMenuConfiguration.SettingsGroupPrefab == null)
            {
                ReportHub.LogError(ReportCategory.SETTINGS_MENU, $"Settings Group prefab not found! Please set it the SettingsMenuConfiguration asset.");
                return;
            }

            GenerateSettingsSection(settingsMenuConfiguration.GeneralSectionConfig, view.GeneralSectionContainer);
            GenerateSettingsSection(settingsMenuConfiguration.GraphicsSectionConfig, view.GraphicsSectionContainer);
            GenerateSettingsSection(settingsMenuConfiguration.SoundSectionConfig, view.SoundSectionContainer);
            GenerateSettingsSection(settingsMenuConfiguration.ControlsSectionConfig, view.ControlsSectionContainer);
            GenerateSettingsSection(settingsMenuConfiguration.ChatSectionConfig, view.ChatSectionContainer);

            foreach (var controller in controllers)
                controller.OnAllControllersInstantiated(controllers);

            SetInitialSectionsVisibility();
        }

        private void GenerateSettingsSection(SettingsSectionConfig sectionConfig, Transform sectionContainer)
        {
            foreach (SettingsGroup group in sectionConfig.SettingsGroups)
            {
                SettingsGroupView generalGroupView = Object.Instantiate(settingsMenuConfiguration.SettingsGroupPrefab, sectionContainer);
                generalGroupView.GroupTitle.text = group.GroupTitle;

                foreach (SettingsModuleBindingBase module in group.Modules)
                    controllers.Add(module?.CreateModule(generalGroupView.ModulesContainer, realmPartitionSettingsAsset, videoPrioritizationSettings, landscapeData, generalAudioMixer, qualitySettingsAsset, controlsSettingsAsset, chatAudioSettingsAsset, memoryCap, userBlockingCacheProxy, worldVolumeMacBus));
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
