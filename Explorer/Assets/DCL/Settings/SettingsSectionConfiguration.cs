using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Settings
{
    [CreateAssetMenu(menuName = "Settings Menu/Create Settings Section Configuration", fileName = "Settings Section Configuration", order = 0)]
    public class SettingsSectionConfiguration : ScriptableObject
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
