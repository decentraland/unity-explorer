using DCL.Settings.ModuleViews;
using System;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class FpsLimitSettingsController : SettingsFeatureController
    {
        private const string FPS_LIMIT_DATA_STORE_KEY = "Settings_FpsLimit";

        private readonly SettingsDropdownModuleView view;

        public FpsLimitSettingsController(SettingsDropdownModuleView view)
        {
            this.view = view;

            if (settingsDataStore.HasKey(FPS_LIMIT_DATA_STORE_KEY))
                view.DropdownView.Dropdown.value = settingsDataStore.GetDropdownValue(FPS_LIMIT_DATA_STORE_KEY);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetFpsLimitSettings);
            SetFpsLimitSettings(view.DropdownView.Dropdown.value);
        }

        private void SetFpsLimitSettings(int index)
        {
            var fpsLimitToApply = 0;
            if (index != 0)
                fpsLimitToApply = Convert.ToInt32(view.DropdownView.Dropdown.options[index].text);

            Application.targetFrameRate = fpsLimitToApply;
            settingsDataStore.SetDropdownValue(FPS_LIMIT_DATA_STORE_KEY, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetFpsLimitSettings);
        }
    }
}
