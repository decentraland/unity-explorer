using DCL.PluginSystem.World;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class CharacterTriggerAreaSettings : IDCLPluginSettings
    {
        [field: Header(nameof(CharacterTriggerAreaPlugin) + "." + nameof(CharacterTriggerAreaSettings))]
        [field: Space]
        [field: SerializeField]
        public AssetReferenceGameObject CharacterTriggerAreaPrefab;
    }
}
