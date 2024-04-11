using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Users
{
    internal class HotUserMarkerObject : MapRendererMarkerBase
    {
        [field: SerializeField]
        internal SpriteRenderer[] spriteRenderers { get; private set; }
    }
}
