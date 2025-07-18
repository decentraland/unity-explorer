using System;
using System.Collections.Generic;
using UnityEngine;
using DCL.FeatureFlags;
using UnityEngine.Serialization;

namespace DCL.Settings.Configuration
{
    [CreateAssetMenu(fileName = "Settings Menu Configuration", menuName = "DCL/Settings/Settings Menu Configuration")]
    public class SettingsMenuConfiguration : ScriptableObject
    {
        [field: SerializeField] internal SettingsGroupView SettingsGroupPrefab { get; set; }
        [field: SerializeField] internal SettingsSectionConfig GeneralSectionConfig { get; set; }
        [field: SerializeField] internal SettingsSectionConfig GraphicsSectionConfig { get; set; }
        [field: SerializeField] internal SettingsSectionConfig SoundSectionConfig { get; set; }
        [field: SerializeField] internal SettingsSectionConfig ControlsSectionConfig { get; set; }
        [field: SerializeField] internal SettingsSectionConfig ChatSectionConfig { get; set; }
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

        [field: SerializeField] internal FeatureId FeatureFlagName { get; set; }

        [field: SerializeReference] [field: SubclassSelector] internal List<SettingsModuleBindingBase> Modules { get; set; }
    }
}
