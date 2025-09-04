using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class WindowModeSettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;

        public WindowModeSettingsController(SettingsDropdownModuleView view)
        {
            this.view = view;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_WINDOW_MODE))
                view.DropdownView.Dropdown.value = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_WINDOW_MODE);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetWindowModeSettings);
            SetWindowModeSettings(view.DropdownView.Dropdown.value);
        }

        private void SetWindowModeSettings(int index)
        {
            Screen.fullScreenMode = FullscreenModeUtils.Modes[index];
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_WINDOW_MODE, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetWindowModeSettings);
        }
    }
}
