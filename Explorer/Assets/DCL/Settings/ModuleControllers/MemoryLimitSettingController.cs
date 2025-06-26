using DCL.Optimization.PerformanceBudgeting;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using ECS.SceneLifeCycle.IncreasingRadius;
using System;
using UnityEngine;
using SystemInfo = UnityEngine.Device.SystemInfo;

namespace DCL.Settings.ModuleControllers
{
    public class MemoryLimitSettingController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly ISystemMemoryCap systemMemoryCap;
        private readonly SceneLoadingLimit sceneLoadingLimit;

        public MemoryLimitSettingController(SettingsDropdownModuleView view, ISystemMemoryCap systemMemoryCap, SceneLoadingLimit sceneLoadingLimit)
        {
            this.view = view;
            this.systemMemoryCap = systemMemoryCap;
            this.sceneLoadingLimit = sceneLoadingLimit;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MEMORY_CAP))
                view.DropdownView.Dropdown.value = DCLPlayerPrefs.GetDropdownValue(DCLPrefKeys.SETTINGS_MEMORY_CAP) < view.DropdownView.Dropdown.options.Count ? DCLPlayerPrefs.GetDropdownValue(DCLPrefKeys.SETTINGS_MEMORY_CAP) : DefaultMemoryCap();
            else
                view.DropdownView.Dropdown.value = DefaultMemoryCap();

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetMemoryLimitSettings);
            SetMemoryLimitSettings(view.DropdownView.Dropdown.value);
        }

        //The default value is the minimum that comes form the SO
        private int DefaultMemoryCap() =>
            view.DropdownView.Dropdown.options.Count - 1;

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetMemoryLimitSettings);
        }

        private void SetMemoryLimitSettings(int index)
        {
            //Index at 0 means Max Memory
            if (index == 0)
                systemMemoryCap.MemoryCap = -1;
            else
            {
                var newCap = Convert.ToInt32(view.DropdownView.Dropdown.options[index].text);
                systemMemoryCap.MemoryCap = newCap;
            }

            sceneLoadingLimit.UpdateMemoryCap();

            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_MEMORY_CAP, index, save: true);
        }
    }
}
