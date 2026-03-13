using System;
using System.Collections.Generic;
using DCL.Quality;
using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;
using TMPro;

namespace DCL.Settings.ModuleControllers
{
    public class ShadowsQualitySettingsController : SettingsFeatureController
    {
        private static readonly ShadowQualityLevel[] LEVELS = { ShadowQualityLevel.Low, ShadowQualityLevel.Medium, ShadowQualityLevel.High };

        private readonly SettingsDropdownModuleView view;
        private readonly IQualitySettingsController qualitySettingsController;

        public ShadowsQualitySettingsController(SettingsDropdownModuleView view, IQualitySettingsController qualitySettingsController)
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
            view.DropdownView.Dropdown.SetValueWithoutNotify(LevelToIndex(qualitySettingsController.SceneShadowQuality));
        }

        private void OnPresetChanged(QualityPresetLevel _)
        {
            view.DropdownView.Dropdown.SetValueWithoutNotify(LevelToIndex(qualitySettingsController.SceneShadowQuality));
        }

        private void OnDropdownValueChanged(int index)
        {
            if (index >= 0 && index < LEVELS.Length)
                qualitySettingsController.SetShadowQuality(LEVELS[index]);
        }

        private static int LevelToIndex(ShadowQualityLevel level)
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