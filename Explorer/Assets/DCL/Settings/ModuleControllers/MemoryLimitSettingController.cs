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

            view.DropdownView.Dropdown.value = settingsDataStore.HasKey(MEMORY_CAP_DATA_STORE_KEY)
                ? settingsDataStore.GetDropdownValue(MEMORY_CAP_DATA_STORE_KEY)
                : GetIndexFromMemoryCap(systemMemoryCap.MemoryCapInMB);

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetMemoryLimitSettings);

            SetMemoryLimitSettings(view.DropdownView.Dropdown.value);
        }

        private int GetIndexFromMemoryCap(long memoryCapInMB)
        {
            if (memoryCapInMB == SystemInfo.systemMemorySize)
                return 0;

            long capInGb = memoryCapInMB / 1024;

            for (var i = 0; i < view.DropdownView.Dropdown.options.Count; i++)
                if (int.TryParse(view.DropdownView.Dropdown.options[i].text, out int result)
                    && result == capInGb)
                    return i;

            return 2;
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
