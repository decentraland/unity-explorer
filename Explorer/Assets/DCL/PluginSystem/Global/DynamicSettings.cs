using DCL.AssetsProvision;
using DCL.Character;
using DCL.PluginSystem;
using Diagnostics.ReportsHandling;
using ECS.Prioritization;
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
        public AssetReferenceGameObject MapRendererConfiguration { get; private set; }

        [field: SerializeField]
        public AssetReferenceGameObject MapCameraObject { get; private set; }

        [field: SerializeField]
        public AssetReferenceGameObject AtlasChunk { get; private set; }

        [field: SerializeField]
        public AssetReferenceGameObject ParcelHighlight { get; private set; }

    }
}
