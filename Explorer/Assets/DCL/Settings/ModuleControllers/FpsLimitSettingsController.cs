using DCL.Prefs;
using DCL.Settings.ModuleViews;
using System;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class FpsLimitSettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;

        public FpsLimitSettingsController(SettingsDropdownModuleView view)
        {
            this.view = view;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_FPS_LIMIT))
                view.DropdownView.Dropdown.value = DCLPlayerPrefs.GetDropdownValue(DCLPrefKeys.SETTINGS_FPS_LIMIT);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetFpsLimitSettings);
            SetFpsLimitSettings(view.DropdownView.Dropdown.value);
        }

        private void SetFpsLimitSettings(int index)
        {
            var fpsLimitToApply = 0;
            if (index != 0)
                fpsLimitToApply = Convert.ToInt32(view.DropdownView.Dropdown.options[index].text);

            Application.targetFrameRate = fpsLimitToApply;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_FPS_LIMIT, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetFpsLimitSettings);
        }
    }
}
