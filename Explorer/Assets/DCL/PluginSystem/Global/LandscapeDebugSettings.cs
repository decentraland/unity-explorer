using System;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class LandscapeDebugSettings : IDCLPluginSettings
    {
        [field: Header(nameof(LandscapeDebugSettings))] [field: Space]
        [field: SerializeField] public StaticSettings.RealmPartitionSettingsRef realmPartitionSettings;
        [field: SerializeField] public LandscapeSettings.LandscapeDataRef landscapeData;
    }
}
