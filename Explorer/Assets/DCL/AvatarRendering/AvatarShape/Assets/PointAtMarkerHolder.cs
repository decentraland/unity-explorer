using System;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Assets
{
    public class PointAtMarkerHolder : MonoBehaviour
    {
        [field: SerializeField] public SpriteRenderer SpriteRenderer { get; private set; }
        [field: SerializeField] public SizeRangeData MinData { get; private set; }
        [field: SerializeField] public SizeRangeData MaxData { get; private set; }

        [Serializable]
        public struct SizeRangeData
        {
            public float Size;
            public float DistanceUnit;
        }

        private string lastProfileId;

        public void Setup(Sprite sprite, string profileId, float sqrDistance)
        {
            float distance = Mathf.Sqrt(sqrDistance);
            float size;
            if (distance < MinData.DistanceUnit)
                size = MinData.Size;
            else if (distance > MaxData.DistanceUnit)
                size = MaxData.Size;
            else
            {
                float t = Mathf.InverseLerp(MinData.DistanceUnit, MaxData.DistanceUnit, distance);
                size = Mathf.Lerp(MinData.Size, MaxData.Size, t);
            }
            transform.localScale = new Vector3(size, size, 1f);

            if (lastProfileId == profileId)
                return;

            lastProfileId = profileId;
            SpriteRenderer.sprite = sprite;
        }

        public void ResetState()
        {
            lastProfileId = null;
            SpriteRenderer.sprite = null;
        }
    }
}
