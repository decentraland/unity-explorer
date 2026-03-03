using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Assets
{
    public class PointAtMarkerHolder : MonoBehaviour
    {
        [field: SerializeField] public SpriteRenderer SpriteRenderer { get; private set; }

        private string lastProfileId;

        public void Setup(Sprite sprite, string profileId)
        {
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
