using DCL.Prefs;
using DCL.Settings.ModuleViews;
using System;
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
                view.DropdownView.Dropdown.value = DCLPlayerPrefs.GetDropdownValue(DCLPrefKeys.SETTINGS_WINDOW_MODE);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetWindowModeSettings);
            SetWindowModeSettings(view.DropdownView.Dropdown.value);
        }

        private void SetWindowModeSettings(int index)
        {
            Screen.fullScreenMode = index switch
                                    {
                                        0 => // Windowed
                                            FullScreenMode.Windowed,
                                        1 => // Fullscreen Borderless
                                            FullScreenMode.FullScreenWindow,
                                        2 => // Fullscreen
                                            FullScreenMode.ExclusiveFullScreen,
                                        _ => throw new ArgumentOutOfRangeException(),
                                    };

            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_WINDOW_MODE, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetWindowModeSettings);
        }
    }
}
