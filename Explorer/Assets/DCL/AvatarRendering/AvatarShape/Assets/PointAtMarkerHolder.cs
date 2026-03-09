using System;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Assets
{
    public class PointAtMarkerHolder : MonoBehaviour
    {
        private static readonly int BACKGROUND_COLOR_ID = Shader.PropertyToID("_BackgroundColor");
        private static readonly int UV_RECT_ID = Shader.PropertyToID("_UVRect");

        [field: SerializeField] public SpriteRenderer SpriteRenderer { get; private set; }
        [field: SerializeField] public SizeRangeData MinData { get; private set; }
        [field: SerializeField] public SizeRangeData MaxData { get; private set; }

        [Serializable]
        public struct SizeRangeData
        {
            public float Size;
            public float DistanceUnit;
        }

        private MaterialPropertyBlock mpb;
        private string lastProfileId;

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
        }

        public void Setup(Sprite sprite, Color backgroundColor, string profileId, float sqrDistance)
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

            mpb.SetColor(BACKGROUND_COLOR_ID, backgroundColor);
            mpb.SetVector(UV_RECT_ID, ComputeUVRect(sprite));
            SpriteRenderer.SetPropertyBlock(mpb);
        }

        public void ResetState()
        {
            lastProfileId = null;
            SpriteRenderer.sprite = null;
        }

        private static Vector4 ComputeUVRect(Sprite sprite)
        {
            Rect texRect = sprite.textureRect;
            float texW = sprite.texture.width;
            float texH = sprite.texture.height;
            return new Vector4(texRect.x / texW, texRect.y / texH, texRect.width / texW, texRect.height / texH);
        }
    }
}
