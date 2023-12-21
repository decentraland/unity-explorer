using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class LandscapeDebugSettings : IDCLPluginSettings
    {
        [field: Header(nameof(LandscapeSettings))] [field: Space]
        [field: SerializeField]
        internal StaticSettings.RealmPartitionSettingsRef realmPartitionSettings { get; }
    }
}
