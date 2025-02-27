using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ProfileElements
{
    public class ProfileThumbnailView : ViewBase, IView
    {
        [field: SerializeField] public Image ThumbnailBackground { get; private set; }
        [field: SerializeField] public ImageView ThumbnailImageView { get; private set; }
        [field: SerializeField] private Sprite defaultEmptyThumbnail;

        public void Setup(Sprite userThumbnail, Color userColor)
        {
            ThumbnailImageView.SetImage(userThumbnail ?? defaultEmptyThumbnail);
            ThumbnailBackground.color = userColor;
        }
    }
}
