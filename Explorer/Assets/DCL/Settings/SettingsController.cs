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
            view.GeneralSectionContainer.gameObject.SetActive(true);
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
            foreach (SettingsGroup group in view.GeneralSectionConfiguration.SettingsGroups)
            {
                SettingsGroupView generalGroupView = Object.Instantiate(view.SettingsGroupPrefab, view.GeneralSectionContainer);
                generalGroupView.GroupTitle.text = group.groupTitle;

                foreach (SettingsModule module in group.modules)
                {
                    SettingsModuleView moduleView = Object.Instantiate(view.SettingsModulesMapping.GetModuleView(module.moduleType), generalGroupView.ModulesContainer).GetComponent<SettingsModuleView>();
                    moduleView.ModuleTitle.text = module.moduleName;
                }
            }
        }
    }
}
