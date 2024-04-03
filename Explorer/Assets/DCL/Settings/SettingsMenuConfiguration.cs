using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Settings
{
    [CreateAssetMenu(menuName = "Settings Menu/Create Settings Menu Configuration", fileName = "Settings Menu Configuration", order = 0)]
    public class SettingsMenuConfiguration : ScriptableObject
    {
        [field: SerializeField] public List<SettingsModuleFeatureMap> SettingsFeaturesMapping { get; set; }
        [field: SerializeField] public SettingsGroupView SettingsGroupPrefab { get; private set; }
        [field: SerializeField] public SettingsSectionConfig GeneralSectionConfig { get; set; }
        [field: SerializeField] public SettingsSectionConfig GraphicsSectionConfig { get; set; }
        [field: SerializeField] public SettingsSectionConfig SoundSectionConfig { get; set; }
        [field: SerializeField] public SettingsSectionConfig ControlsSectionConfig { get; set; }

        public SettingsModuleView GetModuleView(SettingsModuleFeature feature)
        {
            foreach (var map in SettingsFeaturesMapping)
            {
                if (map.moduleFeature == feature)
                    return map.moduleView;
            }

            return null;
        }
    }

    [Serializable]
    public class SettingsModuleFeatureMap
    {
        public SettingsModuleFeature moduleFeature;
        public SettingsModuleView moduleView;
    }

    public enum SettingsModuleFeature
    {
        ExampleToggleSetting,
        ExampleSliderSetting,
        ExampleDropdownSetting,
        // ...
        // rest of the features!
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
        public SettingsModuleFeature moduleFeature;
        public string moduleName;
        public string moduleAlternativeName;
    }
}
