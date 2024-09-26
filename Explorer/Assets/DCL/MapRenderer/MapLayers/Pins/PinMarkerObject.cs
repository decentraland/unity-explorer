using DG.Tweening;
using System;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Pins
{
    public class PinMarkerObject : MapRendererMarkerBase
    {
        private const int SPRITE_SIZE = 36;
        private static readonly Vector3 setAsDestinationPosition = new (0,0.6f,0);
        [field: SerializeField] internal SpriteRenderer mapPinIcon { get; private set; }
        [field: SerializeField] internal SpriteRenderer mapPinIconOutline { get; private set; }
        [field: SerializeField] internal SpriteRenderer[] renderers { get; private set; }
        [field: SerializeField] internal GameObject destinationBackground { get; private set; }
        [field: SerializeField] internal GameObject destinationAnimationElipse { get; private set; }
        [field: SerializeField] internal Transform selectionScalingParent { get; private set; }
        [field: SerializeField] internal Transform pulseScalingParent { get; private set; }


        public void SetScale(float newScale)
        {
            transform.localScale = new Vector3(newScale, newScale, 1f);
        }

        public void SetTexture(Texture2D? texture)
        {
            if (texture != null)
                mapPinIcon.sprite = Sprite.Create(texture, new Rect(0, 0, SPRITE_SIZE, SPRITE_SIZE), VectorUtilities.OneHalf, 50, 0, SpriteMeshType.FullRect, Vector4.one, false);
        }

        public void SetAsDestination(bool isDestination)
        {
            pulseScalingParent.localPosition = isDestination ? setAsDestinationPosition : Vector3.zero;
            destinationBackground.SetActive(isDestination);
            destinationAnimationElipse.SetActive(isDestination);
        }

        public void SetVisibility(bool visible, Action? onFinish = null)
        {
            DOTween.Kill(this);
            Sequence sequence = DOTween.Sequence(this);

            float startAlpha = visible ? 0f : 1f;
            float endAlpha = visible ? 1f : 0f;

            foreach (var renderer in renderers)
            {
                var color = renderer.color;
                color.a = startAlpha;
                renderer.color = color;
                sequence.Join(renderer.DOFade(endAlpha, 0.3f));
            }

            if (onFinish != null)
            {
                sequence.OnComplete(() => onFinish());
            }
        }

    }
}
