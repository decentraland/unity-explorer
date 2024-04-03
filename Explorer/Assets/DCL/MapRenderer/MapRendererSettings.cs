using DCL.AssetsProvision;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.MapRenderer
{
    [Serializable]
    public class MapRendererSettings
    {
        public const int ATLAS_CHUNK_SIZE = 1020;
        public const int PARCEL_SIZE = 20;
        // it is quite expensive to disable TextMeshPro so larger bounds should help keeping the right balance
        public const float CULLING_BOUNDS_IN_PARCELS = 10;

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

        [field: SerializeField]
        public AssetReferenceGameObject SceneOfInterestMarker { get; private set; }

        [field: SerializeField]
        public AssetReferenceGameObject FavoriteMarker { get; private set; }

        [Serializable]
        public class SpriteRendererRef : ComponentReference<SpriteRenderer>
        {
            public SpriteRendererRef(string guid) : base(guid) { }
        }
    }
}
