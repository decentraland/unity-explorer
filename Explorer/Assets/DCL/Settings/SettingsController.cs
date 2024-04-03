using DCL.UI;
using UnityEngine;

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

            view.GeneralSectionButton.onClick.AddListener(() =>
            {
                view.GeneralSectionContainer.gameObject.SetActive(view.Configuration.GeneralSectionConfig.SettingsGroups.Count > 0);
                view.GraphicsSectionContainer.gameObject.SetActive(false);
                view.AudioSectionContainer.gameObject.SetActive(false);
                view.ContentScrollRect.verticalNormalizedPosition = 1;
            });

            view.GraphicsSectionButton.onClick.AddListener(() =>
            {
                view.GeneralSectionContainer.gameObject.SetActive(false);
                view.GraphicsSectionContainer.gameObject.SetActive(view.Configuration.GraphicsSectionConfig.SettingsGroups.Count > 0);
                view.AudioSectionContainer.gameObject.SetActive(false);
                view.ContentScrollRect.verticalNormalizedPosition = 1;
            });

            view.AudioSectionButton.onClick.AddListener(() =>
            {
                view.GeneralSectionContainer.gameObject.SetActive(false);
                view.GraphicsSectionContainer.gameObject.SetActive(false);
                view.AudioSectionContainer.gameObject.SetActive(view.Configuration.AudioSectionConfig.SettingsGroups.Count > 0);
                view.ContentScrollRect.verticalNormalizedPosition = 1;
            });
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
            GenerateSettingsSection(view.Configuration.AudioSectionConfig, view.AudioSectionContainer);

            view.GeneralSectionContainer.gameObject.SetActive(true);
            view.GraphicsSectionContainer.gameObject.SetActive(false);
            view.AudioSectionContainer.gameObject.SetActive(false);
        }

        private void GenerateSettingsSection(SettingsSectionConfig sectionConfig, Transform sectionContainer)
        {
            foreach (SettingsGroup group in sectionConfig.SettingsGroups)
            {
                SettingsGroupView generalGroupView = Object.Instantiate(view.Configuration.SettingsGroupPrefab, sectionContainer);
                generalGroupView.GroupTitle.text = group.groupTitle;

                foreach (SettingsModule module in group.modules)
                {
                    SettingsModuleView moduleView = Object.Instantiate(view.Configuration.GetModuleView(module.moduleType), generalGroupView.ModulesContainer).GetComponent<SettingsModuleView>();
                    moduleView.ModuleTitle.text = module.moduleName;
                }
            }
        }
    }
}
