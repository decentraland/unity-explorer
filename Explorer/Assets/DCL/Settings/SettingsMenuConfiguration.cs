using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Settings
{
    [CreateAssetMenu(menuName = "Create Settings Menu Configuration", fileName = "Settings Menu Configuration", order = 0)]
    public class SettingsMenuConfiguration : ScriptableObject
    {
        [field: SerializeField] public List<SettingsModuleMap> SettingsModulesMapping { get; set; }
        [field: SerializeField] public SettingsGroupView SettingsGroupPrefab { get; private set; }
        [field: SerializeField] public SettingsSectionConfig GeneralSectionConfig { get; set; }
        [field: SerializeField] public SettingsSectionConfig GraphicsSectionConfig { get; set; }
        [field: SerializeField] public SettingsSectionConfig SoundSectionConfig { get; set; }
        [field: SerializeField] public SettingsSectionConfig ControlsSectionConfig { get; set; }

        public SettingsModuleView GetModuleView(SettingsModuleType moduleType)
        {
            foreach (var settingsModule in SettingsModulesMapping)
            {
                if (settingsModule.moduleType == moduleType)
                    return settingsModule.moduleView;
            }

            return null;
        }
    }

    [Serializable]
    public class SettingsModuleMap
    {
        public SettingsModuleType moduleType;
        public SettingsModuleView moduleView;
    }

    [Serializable]
    public class SettingsSectionConfig
    {
        [field: SerializeField] public List<SettingsGroup> SettingsGroups { get; set; }
    }

    [Serializable]
    public class SettingsGroup
    {
        public string groupTitle;
        public List<SettingsModule> modules;
    }

    [Serializable]
    public class SettingsModule
    {
        public SettingsModuleType moduleType;
        public string moduleName;
        public string moduleAlternativeName;
    }

    public enum SettingsModuleType
    {
        Toggle,
        Slider,
        Dropdown,
    }
}
