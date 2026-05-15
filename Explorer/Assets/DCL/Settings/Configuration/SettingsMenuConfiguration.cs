using DCL.AssetsProvision;
using System;
using System.Collections.Generic;
using UnityEngine;
using DCL.FeatureFlags;

namespace DCL.Settings.Configuration
{
    [CreateAssetMenu(fileName = "Settings Menu Configuration", menuName = "DCL/Settings/Settings Menu Configuration")]
    public class SettingsMenuConfiguration : ScriptableObject
    {
        [field: SerializeField] public SettingsGroupViewRef? SettingsGroupPrefab { get; set; }
        [field: SerializeField] internal SettingsSectionConfig GeneralSectionConfig { get; set; } = null!;
        [field: SerializeField] internal SettingsSectionConfig GraphicsSectionConfig { get; set; } = null!;
        [field: SerializeField] internal SettingsSectionConfig SoundSectionConfig { get; set; } = null!;
        [field: SerializeField] internal SettingsSectionConfig ControlsSectionConfig { get; set; } = null!;
        [field: SerializeField] internal SettingsSectionConfig ChatSectionConfig { get; set; } = null!;
    }

    [Serializable]
    public class SettingsSectionConfig
    {
        [field: SerializeField] internal List<SettingsGroup> SettingsGroups { get; set; } = null!;
    }

    [Serializable]
    public class SettingsGroup
    {
        [field: SerializeField] internal string GroupTitle { get; set; } = null!;
        [field: SerializeField] internal FeatureFlag FeatureFlagName { get; set; }
        [field: SerializeField] internal FeatureId FeatureId { get; set; }
        [field: SerializeReference] [field: SubclassSelector] internal List<SettingsModuleBindingBase> Modules { get; set; } = null!;
    }

    [Serializable]
    public class SettingsGroupViewRef : ComponentReference<SettingsGroupView>
    {
        public SettingsGroupViewRef(string guid) : base(guid)
        {
        }
    }
}
