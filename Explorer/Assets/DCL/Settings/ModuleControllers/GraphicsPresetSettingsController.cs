using System;
using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;
using TMPro;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsPresetSettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public GraphicsPresetSettingsController(SettingsDropdownModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            this.view.DropdownView.Dropdown.options.Clear();

            foreach (string name in Enum.GetNames(typeof(QualityPresetLevel)))
                if( name != "Custom")
                    this.view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = name });

            view.DropdownView.Dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            view.DropdownView.Dropdown.placeholder.GetComponent<TMP_Text>().text = "Custom";
            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            OnPresetChanged( qualitySettingsController.CurrentPreset);
        }

        private void OnDropdownValueChanged(int index)
        {
            var level = (QualityPresetLevel)index;

            if (level == QualityPresetLevel.Custom) // Shouldnt be possible
                return;

            qualitySettingsController.SetPreset(level);
        }

        private void OnPresetChanged(QualityPresetLevel level)
        {
            if (level == QualityPresetLevel.Custom)
            {
                view.DropdownView.Dropdown.SetValueWithoutNotify(-1); // If a Placeholder reference is set, this will enable the placeholder and unmark any selected option
                return;
            }
            view.DropdownView.Dropdown.SetValueWithoutNotify((int)level);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}
