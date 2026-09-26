using System;
using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;
using TMPro;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsPresetSettingsController : BaseQualitySettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;

        public GraphicsPresetSettingsController(SettingsDropdownModuleView view, IQualitySettingsController qualitySettingsController) : base(qualitySettingsController)
        {
            this.view = view;

            this.view.DropdownView.Dropdown.options.Clear();

            foreach (string name in Enum.GetNames(typeof(QualityPresetLevel)))
                if( name != "Custom")
                    this.view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = name });

            view.DropdownView.Dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            view.DropdownView.Dropdown.placeholder.GetComponent<TMP_Text>().text = "Custom";
            OnPresetChanged( qualitySettingsController.CurrentPreset);
        }

        private void OnDropdownValueChanged(int index)
        {
            var level = (QualityPresetLevel)index;

            if (level == QualityPresetLevel.Custom) // Shouldnt be possible
                return;

            qualitySettingsController.SetPreset(level);
        }

        protected override void OnPresetChanged(QualityPresetLevel level)
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
            base.Dispose();
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        }
    }
}