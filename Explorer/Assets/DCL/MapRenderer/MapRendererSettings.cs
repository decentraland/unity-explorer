using DCL.AssetsProvision;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.MapRenderer
{
    [Serializable]
    public class MapRendererSettings
    {
        [field: SerializeField]
        public AssetReferenceGameObject MapRendererConfiguration { get; private set; }

        [field: SerializeField]
        public AssetReferenceGameObject MapCameraObject { get; private set; }

        [field: SerializeField]
        public SpriteRendererRef AtlasChunk { get; private set; }

        [field: SerializeField]
        public AssetReferenceGameObject ParcelHighlight { get; private set; }

        [field: SerializeField]
        public AssetReferenceGameObject PlayerMarker { get; private set; }

        [Serializable]
        public class SpriteRendererRef : ComponentReference<SpriteRenderer>
        {
            public SpriteRendererRef(string guid) : base(guid) { }
        }
    }
}
