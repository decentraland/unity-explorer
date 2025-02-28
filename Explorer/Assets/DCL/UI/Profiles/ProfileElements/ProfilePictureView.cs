using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ProfileElements
{
    public class ProfilePictureView : SimpleView<Color>
    {
        [field: SerializeField] public ImageView ThumbnailImageView { get; private set; }
        [SerializeField] private Image thumbnailBackground;
        [SerializeField] private Sprite defaultEmptyThumbnail;

        public override void Setup(Color userColor)
        {
            thumbnailBackground.color = userColor;
        }
    }
}
