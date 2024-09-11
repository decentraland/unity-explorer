using TMPro;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.PointsOfInterest
{
    public class SceneOfInterestMarkerObject : MapRendererMarkerBase
    {
        private const float Y_POSITION_OFFSET = -0.2f;
        [field: SerializeField] internal TextMeshPro title { get; set; }
        [field: SerializeField] internal SpriteRenderer[] renderers { get; private set; }
        private Vector3 titleBasePosition;

        private float titleBaseScale;

        private void Awake()
        {
            titleBaseScale = title.transform.localScale.x;
            titleBasePosition = title.transform.localPosition;
        }

        public void SetScale(float baseScale, float newScale)
        {
            transform.localScale = new Vector3(newScale, newScale, 1f);

            // Apply inverse scaling to the text object
            float positionFactor = newScale / baseScale;
            float yOffset = (1 - positionFactor) * Y_POSITION_OFFSET;

            float yValue = yOffset < 0.9f
                ? titleBasePosition.y + yOffset
                : titleBasePosition.y / positionFactor;

            title.transform.localPosition = new Vector3(titleBasePosition.x, yValue, titleBasePosition.z);

            float textScaleFactor = baseScale / newScale; // Calculate the inverse scale factor
            title.transform.localScale = new Vector3(titleBaseScale * textScaleFactor, titleBaseScale * textScaleFactor, 1f);
        }
    }
}
