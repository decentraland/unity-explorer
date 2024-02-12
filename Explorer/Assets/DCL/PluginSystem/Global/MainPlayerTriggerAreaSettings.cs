using DCL.PluginSystem.World;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class MainPlayerTriggerAreaSettings : IDCLPluginSettings
    {
        [field: Header(nameof(MainPlayerTriggerAreaPlugin) + "." + nameof(MainPlayerTriggerAreaSettings))]
        [field: Space]
        [field: SerializeField]
        public AssetReferenceGameObject MainPlayerTriggerAreaPrefab;
    }

    // [Serializable]
    // public class MainPlayerTriggerAreaReference : ComponentReference<AssetReferenceGameObject>
    // {
    //     public MainPlayerTriggerAreaReference(string guid) : base(guid) { }
    // }
}
