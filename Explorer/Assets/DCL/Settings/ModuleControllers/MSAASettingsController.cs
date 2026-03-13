using System;
using System.Collections.Generic;
using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;
using TMPro;

namespace DCL.Settings.ModuleControllers
{
    public class MSAASettingsController : SettingsFeatureController
    {
        private static readonly MsaaLevel[] LEVELS = { MsaaLevel.Off, MsaaLevel.X2, MsaaLevel.X4, MsaaLevel.X8 };

        private readonly SettingsDropdownModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public MSAASettingsController(SettingsDropdownModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            qualitySettingsController.OnPresetChanged += OnPresetChanged;
            view.DropdownView.Dropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>(LEVELS.Length);
            for (int i = 0; i < LEVELS.Length; i++)
                options.Add(new TMP_Dropdown.OptionData(LEVELS[i].ToString()));
            view.DropdownView.Dropdown.AddOptions(options);
            view.DropdownView.Dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            view.DropdownView.Dropdown.SetValueWithoutNotify(MsaaLevelToIndex(qualitySettingsController.Msaa));
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            view.DropdownView.Dropdown.SetValueWithoutNotify(MsaaLevelToIndex(qualitySettingsController.Msaa));
        }

        private void OnDropdownValueChanged(int index)
        {
            if (index >= 0 && index < LEVELS.Length)
                qualitySettingsController.SetMsaa(LEVELS[index]);
        }

        private static int MsaaLevelToIndex(MsaaLevel level)
        {
            int index = Array.IndexOf(LEVELS, level);
            return index >= 0 ? index : 0;
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            qualitySettingsController.OnPresetChanged -= OnPresetChanged;
        }
    }
}
