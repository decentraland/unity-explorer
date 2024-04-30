using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.Quality;
using DCL.Settings.Configuration;
using DCL.Settings.ModuleControllers;
using DCL.UI;
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
        private readonly SettingsView view;
        private readonly SettingsMenuConfiguration settingsMenuConfiguration;
        private readonly AudioMixer generalAudioMixer;
        private readonly RealmPartitionSettingsAsset realmPartitionSettingsAsset;
        private readonly LandscapeData landscapeData;
        private readonly QualitySettingsAsset qualitySettingsAsset;
        private readonly RectTransform rectTransform;
        private readonly List<SettingsFeatureController> controllers = new ();

        public SettingsController(
            SettingsView view,
            SettingsMenuConfiguration settingsMenuConfiguration,
            AudioMixer generalAudioMixer,
            RealmPartitionSettingsAsset realmPartitionSettingsAsset,
            LandscapeData landscapeData,
            QualitySettingsAsset qualitySettingsAsset)
        {
            this.view = view;
            this.settingsMenuConfiguration = settingsMenuConfiguration;
            this.generalAudioMixer = generalAudioMixer;
            this.realmPartitionSettingsAsset = realmPartitionSettingsAsset;
            this.landscapeData = landscapeData;
            this.qualitySettingsAsset = qualitySettingsAsset;

            rectTransform = view.transform.parent.GetComponent<RectTransform>();

            GenerateSettings();

            view.GeneralSectionButton.Button.onClick.AddListener(OpenGeneralSection);
            view.GraphicsSectionButton.Button.onClick.AddListener(OpenGraphicsSection);
            view.SoundSectionButton.Button.onClick.AddListener(OpenSoundSection);
            view.ControlsSectionButton.Button.onClick.AddListener(OpenControlsSection);
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

            SetInitialSectionsVisibility();
        }

        private void GenerateSettingsSection(SettingsSectionConfig sectionConfig, Transform sectionContainer)
        {
            foreach (SettingsGroup group in sectionConfig.SettingsGroups)
            {
                SettingsGroupView generalGroupView = Object.Instantiate(settingsMenuConfiguration.SettingsGroupPrefab, sectionContainer);
                generalGroupView.GroupTitle.text = group.GroupTitle;

                foreach (SettingsModuleBindingBase module in group.Modules)
                    controllers.Add(module?.CreateModule(generalGroupView.ModulesContainer, realmPartitionSettingsAsset, landscapeData, generalAudioMixer, qualitySettingsAsset));
            }
        }

        private void SetInitialSectionsVisibility()
        {
            view.GeneralSectionButton.gameObject.SetActive(settingsMenuConfiguration.GeneralSectionConfig.SettingsGroups.Count > 0);
            view.GraphicsSectionButton.gameObject.SetActive(settingsMenuConfiguration.GraphicsSectionConfig.SettingsGroups.Count > 0);
            view.SoundSectionButton.gameObject.SetActive(settingsMenuConfiguration.SoundSectionConfig.SettingsGroups.Count > 0);
            view.ControlsSectionButton.gameObject.SetActive(settingsMenuConfiguration.ControlsSectionConfig.SettingsGroups.Count > 0);

            if (settingsMenuConfiguration.GeneralSectionConfig.SettingsGroups.Count > 0)
                OpenGeneralSection();
            else if (settingsMenuConfiguration.GraphicsSectionConfig.SettingsGroups.Count > 0)
                OpenGraphicsSection();
            else if (settingsMenuConfiguration.SoundSectionConfig.SettingsGroups.Count > 0)
                OpenSoundSection();
            else if (settingsMenuConfiguration.ControlsSectionConfig.SettingsGroups.Count > 0)
                OpenControlsSection();
        }

        private void OpenGeneralSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(settingsMenuConfiguration.GeneralSectionConfig.SettingsGroups.Count > 0);
            view.GraphicsSectionContainer.gameObject.SetActive(false);
            view.SoundSectionContainer.gameObject.SetActive(false);
            view.ControlsSectionContainer.gameObject.SetActive(false);
            view.GeneralSectionButton.SetSelected(true);
            view.GraphicsSectionButton.SetSelected(false);
            view.SoundSectionButton.SetSelected(false);
            view.ControlsSectionButton.SetSelected(false);
            view.BackgroundImage.sprite = view.GeneralSectionBackground;
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        private void OpenGraphicsSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(false);
            view.GraphicsSectionContainer.gameObject.SetActive(settingsMenuConfiguration.GraphicsSectionConfig.SettingsGroups.Count > 0);
            view.SoundSectionContainer.gameObject.SetActive(false);
            view.ControlsSectionContainer.gameObject.SetActive(false);
            view.GeneralSectionButton.SetSelected(false);
            view.GraphicsSectionButton.SetSelected(true);
            view.SoundSectionButton.SetSelected(false);
            view.ControlsSectionButton.SetSelected(false);
            view.BackgroundImage.sprite = view.GraphicsSectionBackground;
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        private void OpenSoundSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(false);
            view.GraphicsSectionContainer.gameObject.SetActive(false);
            view.SoundSectionContainer.gameObject.SetActive(settingsMenuConfiguration.SoundSectionConfig.SettingsGroups.Count > 0);
            view.ControlsSectionContainer.gameObject.SetActive(false);
            view.GeneralSectionButton.SetSelected(false);
            view.GraphicsSectionButton.SetSelected(false);
            view.SoundSectionButton.SetSelected(true);
            view.ControlsSectionButton.SetSelected(false);
            view.BackgroundImage.sprite = view.SoundSectionBackground;
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        private void OpenControlsSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(false);
            view.GraphicsSectionContainer.gameObject.SetActive(false);
            view.SoundSectionContainer.gameObject.SetActive(false);
            view.ControlsSectionContainer.gameObject.SetActive(settingsMenuConfiguration.ControlsSectionConfig.SettingsGroups.Count > 0);
            view.GeneralSectionButton.SetSelected(false);
            view.GraphicsSectionButton.SetSelected(false);
            view.SoundSectionButton.SetSelected(false);
            view.ControlsSectionButton.SetSelected(true);
            view.BackgroundImage.sprite = view.ControlsSectionBackground;
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        public void Dispose()
        {
            foreach (SettingsFeatureController controller in controllers)
                controller.Dispose();
        }
    }
}
