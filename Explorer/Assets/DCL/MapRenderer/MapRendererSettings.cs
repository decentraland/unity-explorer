using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCLServices.MapRenderer
{
    [Serializable]
    public class MapRendererSettings
    {
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
