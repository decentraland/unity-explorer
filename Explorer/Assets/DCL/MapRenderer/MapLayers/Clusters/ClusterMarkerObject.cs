using TMPro;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Categories
{
    public class ClusterMarkerObject : MapRendererMarkerBase
    {
        [field: SerializeField] internal TextMeshPro title { get; set; }
        [field: SerializeField] internal SpriteRenderer[] renderers { get; private set; }
        [field: SerializeField] internal SpriteRenderer categorySprite { get; private set; }

        public void SetCategorySprite(Sprite sprite)
        {
            categorySprite.sprite = sprite;
        }

        public void SetScale(float baseScale, float newScale)
        {
            transform.localScale = new Vector3(newScale, newScale, 1f);
        }
    }
}
