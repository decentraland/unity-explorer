using MVC;
using UnityEngine;
using UnityEngine.UI;
using IView = MVC.IView;

namespace DCL.UI.ProfileElements
{
    public class ProfileThumbnailView : SimpleView<Color>
    {
        [field: SerializeField] public ImageView ThumbnailImageView { get; private set; }
        [SerializeField] private Image thumbnailBackground;
        [SerializeField] private Sprite defaultEmptyThumbnail;

        protected override void Setup(Color userColor)
        {
            thumbnailBackground.color = userColor;
        }
    }
}
