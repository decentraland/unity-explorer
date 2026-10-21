using TMPro;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.SearchResults
{
    public class SearchResultMarkerObject : MapRendererMarkerBase
    {
        [field: SerializeField] internal Transform scalingParent { get; set; }
        [field: SerializeField] internal TextMeshPro title { get; set; }
        [field: SerializeField] internal SpriteRenderer[] renderers { get; private set; }
        [field: SerializeField] internal TextMeshPro[] textRenderers { get; private set; }
        [field: SerializeField] internal SpriteRenderer icon { get; private set; }
        [field: SerializeField] internal Sprite toggledSprite { get; private set; }
        [field: SerializeField] internal Sprite regularSprite { get; private set; }

        private Vector3 titleBasePosition;

        private float titleBaseScale;

        private void Awake()
        {
            titleBaseScale = title.transform.localScale.x;
            titleBasePosition = title.transform.localPosition;
        }

        public void SetScale(float baseScale, float newScale) =>
            MarkerHelper.ScaleWithTitle(transform, title.transform, titleBasePosition, titleBaseScale, baseScale, newScale);

        public void ToggleSelection(bool isSelected) =>
            icon.sprite = isSelected ? toggledSprite : regularSprite;
    }
}
