using DCL.Diagnostics;
using DCL.Settings.Configuration;
using DCL.Settings.ModuleControllers;
using DCL.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Settings
{
    public class SettingsController : ISection, IDisposable
    {
        private readonly SettingsView view;
        private readonly RectTransform rectTransform;
        private List<SettingsFeatureController> controllers = new ();

        public SettingsController(SettingsView view)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();

            GenerateSettings();

            view.GeneralSectionButtonWithSelectableState.Button.onClick.AddListener(OpenGeneralSection);
            view.GraphicsSectionButtonWithSelectableState.Button.onClick.AddListener(OpenGraphicsSection);
            view.SoundSectionButtonWithSelectableState.Button.onClick.AddListener(OpenSoundSection);
            view.ControlsSectionButtonWithSelectableState.Button.onClick.AddListener(OpenControlsSection);
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;

        private void GenerateSettings()
        {
            if (view.Configuration.SettingsGroupPrefab == null)
            {
                ReportHub.LogError(ReportCategory.SETTINGS_MENU, $"Settings Group prefab not found! Please set it the SettingsMenuConfiguration asset.");
                return;
            }

            GenerateSettingsSection(view.Configuration.GeneralSectionConfig, view.GeneralSectionContainer);
            GenerateSettingsSection(view.Configuration.GraphicsSectionConfig, view.GraphicsSectionContainer);
            GenerateSettingsSection(view.Configuration.SoundSectionConfig, view.SoundSectionContainer);
            GenerateSettingsSection(view.Configuration.ControlsSectionConfig, view.ControlsSectionContainer);

            OpenGeneralSection();
        }

        private void GenerateSettingsSection(SettingsSectionConfig sectionConfig, Transform sectionContainer)
        {
            foreach (SettingsGroup group in sectionConfig.SettingsGroups)
            {
                SettingsGroupView generalGroupView = Object.Instantiate(view.Configuration.SettingsGroupPrefab, sectionContainer);
                generalGroupView.GroupTitle.text = group.GroupTitle;

                foreach (SettingsModuleBindingBase module in group.Modules)
                    controllers.Add(module?.CreateModule(generalGroupView.ModulesContainer));
            }
        }

        private void OpenGeneralSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(view.Configuration.GeneralSectionConfig.SettingsGroups.Count > 0);
            view.GraphicsSectionContainer.gameObject.SetActive(false);
            view.SoundSectionContainer.gameObject.SetActive(false);
            view.ControlsSectionContainer.gameObject.SetActive(false);
            view.GeneralSectionButtonWithSelectableState.SetSelected(true);
            view.GraphicsSectionButtonWithSelectableState.SetSelected(false);
            view.SoundSectionButtonWithSelectableState.SetSelected(false);
            view.ControlsSectionButtonWithSelectableState.SetSelected(false);
            view.BackgroundImage.sprite = view.GeneralSectionBackground;
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        private void OpenGraphicsSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(false);
            view.GraphicsSectionContainer.gameObject.SetActive(view.Configuration.GraphicsSectionConfig.SettingsGroups.Count > 0);
            view.SoundSectionContainer.gameObject.SetActive(false);
            view.ControlsSectionContainer.gameObject.SetActive(false);
            view.GeneralSectionButtonWithSelectableState.SetSelected(false);
            view.GraphicsSectionButtonWithSelectableState.SetSelected(true);
            view.SoundSectionButtonWithSelectableState.SetSelected(false);
            view.ControlsSectionButtonWithSelectableState.SetSelected(false);
            view.BackgroundImage.sprite = view.GraphicsSectionBackground;
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        private void OpenSoundSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(false);
            view.GraphicsSectionContainer.gameObject.SetActive(false);
            view.SoundSectionContainer.gameObject.SetActive(view.Configuration.SoundSectionConfig.SettingsGroups.Count > 0);
            view.ControlsSectionContainer.gameObject.SetActive(false);
            view.GeneralSectionButtonWithSelectableState.SetSelected(false);
            view.GraphicsSectionButtonWithSelectableState.SetSelected(false);
            view.SoundSectionButtonWithSelectableState.SetSelected(true);
            view.ControlsSectionButtonWithSelectableState.SetSelected(false);
            view.BackgroundImage.sprite = view.SoundSectionBackground;
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        private void OpenControlsSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(false);
            view.GraphicsSectionContainer.gameObject.SetActive(false);
            view.SoundSectionContainer.gameObject.SetActive(false);
            view.ControlsSectionContainer.gameObject.SetActive(view.Configuration.ControlsSectionConfig.SettingsGroups.Count > 0);
            view.GeneralSectionButtonWithSelectableState.SetSelected(false);
            view.GraphicsSectionButtonWithSelectableState.SetSelected(false);
            view.SoundSectionButtonWithSelectableState.SetSelected(false);
            view.ControlsSectionButtonWithSelectableState.SetSelected(true);
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
