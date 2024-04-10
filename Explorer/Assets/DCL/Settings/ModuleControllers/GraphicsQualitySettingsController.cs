using DCL.Settings.ModuleViews;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsQualitySettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;

        public GraphicsQualitySettingsController(SettingsDropdownModuleView view, int defaultQualityLevel)
        {
            this.view = view;

            // Clean current options loaded from the settings menu configuration and load names from QualitySettings
            view.DropdownView.Dropdown.options.Clear();
            foreach (string option in QualitySettings.names)
                view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = option });

            if (view.DropdownView.Dropdown.options.Count > defaultQualityLevel && defaultQualityLevel >= 0)
                view.DropdownView.Dropdown.value = defaultQualityLevel;

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetQualityLevel);
        }

        private void SetQualityLevel(int index)
        {
            QualitySettings.SetQualityLevel(index);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetQualityLevel);
        }
    }
}
