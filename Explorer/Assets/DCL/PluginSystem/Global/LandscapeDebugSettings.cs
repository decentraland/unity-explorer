using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class LandscapeDebugSettings : IDCLPluginSettings
    {
        [field: Header(nameof(LandscapeSettings))] [field: Space]
        [field: SerializeField]
        public StaticSettings.RealmPartitionSettingsRef realmPartitionSettings;

        [field: Header(nameof(LandscapeSettings))]
        [field: Space]
        [field: SerializeField] internal LandscapeSettings.LandscapeDataRef landscapeData { get; }
    }
}
