using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    public class AtlasChunk : MonoBehaviour
    {
        [field: SerializeField]
        public SpriteRenderer LoadingSpriteRenderer { get; private set; }

        [field: SerializeField]
        public SpriteRenderer MainSpriteRenderer { get; private set; }
    }
}
