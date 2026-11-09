using TMPro;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Categories
{
    public class CategoryMarkerObject : MapRendererMarkerBase
    {
        [field: SerializeField] internal Transform scalingParent { get; set; }
        [field: SerializeField] internal TextMeshPro title { get; set; }
        [field: SerializeField] internal SpriteRenderer[] renderers { get; private set; }
        [field: SerializeField] internal TextMeshPro[] textRenderers { get; private set; }
        [field: SerializeField] internal SpriteRenderer categorySprite { get; private set; }

        [field: SerializeField] internal Sprite toggledSprite { get; private set; }

        private Sprite regularSprite;

        private Vector3 titleBasePosition;

        private float titleBaseScale;

        private void Awake()
        {
            titleBaseScale = title.transform.localScale.x;
            titleBasePosition = title.transform.localPosition;
        }

        public void SetCategorySprite(Sprite sprite)
        {
            regularSprite = sprite;
            categorySprite.sprite = sprite;
        }

        public void SetScale(float baseScale, float newScale) =>
            MarkerHelper.ScaleWithTitle(transform, title.transform, titleBasePosition, titleBaseScale, baseScale, newScale);

        public void ToggleSelection(bool isSelected) =>
            categorySprite.sprite = isSelected ? toggledSprite : regularSprite;
    }
}
