using System;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.UsersMarker
{
    public class HotUserMarkerObject : MapRendererMarkerBase
    {
        [field: SerializeField]
        private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();

        public void UpdateSortOrder(int sortingOrder)
        {
            for (var i = 0; i < spriteRenderers.Length; i++)
                spriteRenderers[i].sortingOrder = sortingOrder;
        }
    }
}
