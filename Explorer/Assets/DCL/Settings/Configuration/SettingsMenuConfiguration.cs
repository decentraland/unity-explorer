using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Settings.Configuration
{
    [CreateAssetMenu(menuName = "Settings Menu/Create Settings Menu Configuration", fileName = "Settings Menu Configuration", order = 0)]
    public class SettingsMenuConfiguration : ScriptableObject
    {
        [field: SerializeField] internal SettingsGroupView SettingsGroupPrefab { get; set; }
        [field: SerializeField] internal SettingsSectionConfig GeneralSectionConfig { get; set; }
        [field: SerializeField] internal SettingsSectionConfig GraphicsSectionConfig { get; set; }
        [field: SerializeField] internal SettingsSectionConfig SoundSectionConfig { get; set; }
        [field: SerializeField] internal SettingsSectionConfig ControlsSectionConfig { get; set; }
    }

    [Serializable]
    public class SettingsSectionConfig
    {
        [field: SerializeField] internal List<SettingsGroup> SettingsGroups { get; set; }
    }

    [Serializable]
    public class SettingsGroup
    {
        [field: SerializeField] internal string GroupTitle { get; set; }

        [field: SerializeReference, SubclassSelector] internal List<SettingsModuleBindingBase> Modules { get; set; }
    }
}
