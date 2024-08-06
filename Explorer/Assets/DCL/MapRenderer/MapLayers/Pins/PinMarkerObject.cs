using JetBrains.Annotations;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Pins
{
    public class PinMarkerObject : MapRendererMarkerBase
    {
        private const int SPRITE_SIZE = 36;
        [field: SerializeField] internal SpriteRenderer mapPinIcon { get; private set; }
        [field: SerializeField] internal SpriteRenderer mapPinIconOutline { get; private set; }
        [field: SerializeField] internal SpriteRenderer[] renderers { get; private set; }
        [field: SerializeField] internal GameObject destinationBackground { get; private set; }

        public void SetScale(float newScale)
        {
            transform.localScale = new Vector3(newScale, newScale, 1f);
        }

        public void SetTexture(Texture2D texture)
        {
            mapPinIcon.sprite = Sprite.Create(texture, new Rect(0, 0, SPRITE_SIZE, SPRITE_SIZE), VectorUtilities.OneHalf, 50, 0, SpriteMeshType.FullRect, Vector4.one, false);
        }

        public void SetAsDestination(bool isDestination)
        {
            destinationBackground.SetActive(isDestination);
        }
    }
}
