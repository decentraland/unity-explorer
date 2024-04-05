using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Settings.Configuration
{
    [CreateAssetMenu(menuName = "Settings Menu/Create Settings Menu Configuration", fileName = "Settings Menu Configuration", order = 0)]
    public class SettingsMenuConfiguration : ScriptableObject
    {
        [field: SerializeField] public SettingsGroupView SettingsGroupPrefab { get; private set; }
        [field: SerializeField] public SettingsSectionConfig GeneralSectionConfig { get; set; }
        [field: SerializeField] public SettingsSectionConfig GraphicsSectionConfig { get; set; }
        [field: SerializeField] public SettingsSectionConfig SoundSectionConfig { get; set; }
        [field: SerializeField] public SettingsSectionConfig ControlsSectionConfig { get; set; }
    }

    [Serializable]
    public class SettingsSectionConfig
    {
        [field: SerializeField] public List<SettingsGroup> SettingsGroups { get; set; }
    }

    [Serializable]
    public class SettingsGroup
    {
        [field: SerializeField]
        public string GroupTitle { get; private set; }

        [field: SerializeReference, SubclassSelector]
        public List<SettingsModuleBindingBase> Modules { get; private set; }
    }
}
