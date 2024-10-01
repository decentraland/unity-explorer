using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using UnityEngine;

namespace DCL.Settings.Configuration
{
    public class MemoryLimitSettingController : SettingsFeatureController
    {
        // private const string FPS_LIMIT_DATA_STORE_KEY = "Settings_FpsLimit";

        private readonly SettingsDropdownModuleView view;

        public MemoryLimitSettingController(SettingsDropdownModuleView view)
        {
            this.view = view;

            // if (settingsDataStore.HasKey(FPS_LIMIT_DATA_STORE_KEY))
            //     view.DropdownView.Dropdown.value = settingsDataStore.GetDropdownValue(FPS_LIMIT_DATA_STORE_KEY);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetMemoryLimitSettings);
            // SetMemoryLimitSettings(view.DropdownView.Dropdown.value);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetMemoryLimitSettings);
        }

        private void SetMemoryLimitSettings(int index)
        {
            Debug.Log("VVV memory limit changed");
            // var fpsLimitToApply = 0;
            // if (index != 0)
            //     fpsLimitToApply = Convert.ToInt32(view.DropdownView.Dropdown.options[index].text);
            //
            // Application.targetFrameRate = fpsLimitToApply;
            // settingsDataStore.SetDropdownValue(FPS_LIMIT_DATA_STORE_KEY, index, save: true);
        }
    }
}
