using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using Global.AppArgs;
using System;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class WindowModeSettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly IAppArgs appParameters;

        public WindowModeSettingsController(SettingsDropdownModuleView view, IAppArgs appParameters)
        {
            this.view = view;
            this.appParameters = appParameters;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_WINDOW_MODE))
                view.DropdownView.Dropdown.value = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_WINDOW_MODE);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetWindowModeSettingsOnValueChanged);
            SetWindowModeSettings(view.DropdownView.Dropdown.value, true);
        }

        private void SetWindowModeSettingsOnValueChanged(int index)
        {
            SetWindowModeSettings(index, false);
        }

        private void SetWindowModeSettings(int index, bool initialSetup)
        {
            if (appParameters.HasFlag(AppArgsFlags.WINDOWED_MODE) && initialSetup)
                return;

            Screen.fullScreenMode = FullscreenModeUtils.Modes[index];
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_WINDOW_MODE, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetWindowModeSettingsOnValueChanged);
        }
    }
}
