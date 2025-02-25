using DCL.Optimization.PerformanceBudgeting;
using DCL.Settings.ModuleViews;
using System;
using UnityEngine;
using SystemInfo = UnityEngine.Device.SystemInfo;

namespace DCL.Settings.ModuleControllers
{
    public class MemoryLimitSettingController : SettingsFeatureController
    {
        private const string MEMORY_CAP_DATA_STORE_KEY = "Settings_MemoryCap";

        private readonly SettingsDropdownModuleView view;
        private readonly ISystemMemoryCap systemMemoryCap;

        public MemoryLimitSettingController(SettingsDropdownModuleView view, ISystemMemoryCap systemMemoryCap)
        {
            this.view = view;
            this.systemMemoryCap = systemMemoryCap;

            if (settingsDataStore.HasKey(MEMORY_CAP_DATA_STORE_KEY))
            {
                view.DropdownView.Dropdown.value = settingsDataStore.GetDropdownValue(MEMORY_CAP_DATA_STORE_KEY) < view.DropdownView.Dropdown.options.Count ? settingsDataStore.GetDropdownValue(MEMORY_CAP_DATA_STORE_KEY) : DefaultMemoryCap();
            }
            else
            {
                view.DropdownView.Dropdown.value = DefaultMemoryCap();
            }

            
            view.DropdownView.Dropdown.onValueChanged.AddListener(SetMemoryLimitSettings);
            SetMemoryLimitSettings(view.DropdownView.Dropdown.value);
        }

        //The default value is the minimum that comes form the SO
        private int DefaultMemoryCap()
        {
            return view.DropdownView.Dropdown.options.Count - 1;
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetMemoryLimitSettings);
        }

        private void SetMemoryLimitSettings(int index)
        {
            if (index == 0)
                systemMemoryCap.MemoryCap = SystemInfo.systemMemorySize / 1024;
            else
            {
                var newCap = Convert.ToInt32(view.DropdownView.Dropdown.options[index].text);
                systemMemoryCap.MemoryCap = Mathf.Min(newCap, SystemInfo.systemMemorySize / 1024);
            }

            settingsDataStore.SetDropdownValue(MEMORY_CAP_DATA_STORE_KEY, index, save: true);
        }
    }
}
