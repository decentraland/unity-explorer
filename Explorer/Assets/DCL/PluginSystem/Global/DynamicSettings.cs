using DCL.MapRenderer;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class DynamicSettings : IDCLPluginSettings
    {
        [field: SerializeField]
        public AssetReferenceGameObject PopupCloserView { get; private set; }

        [field: SerializeField]
        public MapRendererSettings MapRendererSettings { get; private set; }
    }
}
