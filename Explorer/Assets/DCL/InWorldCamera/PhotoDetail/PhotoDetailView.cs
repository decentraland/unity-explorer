using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailView : ViewBase, IView
    {
        [SerializeField] internal RectTransform rootContainer;
        [SerializeField] internal float sidePanelAnimationDuration = 0.5f;
        [SerializeField] internal float imageFadeInDuration = 0.3f;
        [SerializeField] internal Image mainImage;
        [SerializeField] internal CanvasGroup mainImageCanvasGroup;
        [SerializeField] internal GameObject mainImageLoadingSpinner;

        [Header("Navigation buttons")]
        [SerializeField] internal Button closeButton;
        [SerializeField] internal Button previousScreenshotButton;
        [SerializeField] internal Button nextScreenshotButton;

        [Header("Actions panel")]
        [SerializeField] internal Button downloadButton;
        [SerializeField] internal Button deleteButton;
        [SerializeField] internal Button linkButton;
        [SerializeField] internal Button twitterButton;
        [SerializeField] internal Button infoButton;
        [SerializeField] internal RectTransform infoButtonImageRectTransform;

        private void Awake()
        {
            mainImage.sprite = null;
            mainImageCanvasGroup.alpha = 0;
        }
    }
}
