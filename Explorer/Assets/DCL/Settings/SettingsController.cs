using DCL.Diagnostics;
using DCL.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Settings
{
    public class SettingsController : ISection
    {
        private readonly SettingsView view;
        private readonly RectTransform rectTransform;

        public SettingsController(SettingsView view)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();

            GenerateSettings();

            view.GeneralSectionButton.onClick.AddListener(OpenGeneralSection);
            view.GraphicsSectionButton.onClick.AddListener(OpenGraphicsSection);
            view.SoundSectionButton.onClick.AddListener(OpenSoundSection);
            view.ControlsSectionButton.onClick.AddListener(OpenControlsSection);
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
                generalGroupView.GroupTitle.text = group.groupTitle;

                foreach (SettingsModule module in group.modules)
                {
                    var moduleViewToInstantiate = view.Configuration.GetModuleView(module.moduleFeature);
                    if (moduleViewToInstantiate == null)
                    {
                        ReportHub.LogError(ReportCategory.SETTINGS_MENU, $"Module view for feature '{module.moduleFeature}' not found! Please set its mapping in the SettingsMenuConfiguration asset.");
                        continue;
                    }

                    SettingsModuleView moduleView = Object.Instantiate(moduleViewToInstantiate, generalGroupView.ModulesContainer).GetComponent<SettingsModuleView>();
                    moduleView.ModuleTitle.text = module.moduleName;

                    switch (module.moduleFeature)
                    {
                        case SettingsModuleFeature.SettingFeature1:
                            new SettingFeature1Controller(moduleView as SettingsToggleModuleView);
                            break;
                        case SettingsModuleFeature.SettingFeature2:
                            new SettingFeature2Controller(moduleView as SettingsSliderModuleView);
                            break;
                        case SettingsModuleFeature.SettingFeature3:
                            new SettingFeature3Controller(moduleView as SettingsDropdownModuleView);
                            break;
                    }
                }
            }
        }

        private void OpenGeneralSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(view.Configuration.GeneralSectionConfig.SettingsGroups.Count > 0);
            view.GraphicsSectionContainer.gameObject.SetActive(false);
            view.SoundSectionContainer.gameObject.SetActive(false);
            view.ControlsSectionContainer.gameObject.SetActive(false);
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        private void OpenGraphicsSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(false);
            view.GraphicsSectionContainer.gameObject.SetActive(view.Configuration.GraphicsSectionConfig.SettingsGroups.Count > 0);
            view.SoundSectionContainer.gameObject.SetActive(false);
            view.ControlsSectionContainer.gameObject.SetActive(false);
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        private void OpenSoundSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(false);
            view.GraphicsSectionContainer.gameObject.SetActive(false);
            view.SoundSectionContainer.gameObject.SetActive(view.Configuration.SoundSectionConfig.SettingsGroups.Count > 0);
            view.ControlsSectionContainer.gameObject.SetActive(false);
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }

        private void OpenControlsSection()
        {
            view.GeneralSectionContainer.gameObject.SetActive(false);
            view.GraphicsSectionContainer.gameObject.SetActive(false);
            view.SoundSectionContainer.gameObject.SetActive(false);
            view.ControlsSectionContainer.gameObject.SetActive(view.Configuration.ControlsSectionConfig.SettingsGroups.Count > 0);
            view.ContentScrollRect.verticalNormalizedPosition = 1;
        }
    }
}
