using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailView : ViewBase, IView
    {
        [field: SerializeField] internal RectTransform rootContainer { get; private set; }
        [field: SerializeField] internal float sidePanelAnimationDuration { get; private set; } = 0.5f;
        [field: SerializeField] internal float imageFadeInDuration { get; private set; } = 0.3f;
        [field: SerializeField] internal Image mainImage { get; private set; }
        [field: SerializeField] internal CanvasGroup mainImageCanvasGroup { get; private set; }
        [field: SerializeField] internal GameObject mainImageLoadingSpinner { get; private set; }

        [field: Header("Navigation buttons")]
        [field: SerializeField] internal Button closeButton { get; private set; }
        [field: SerializeField] internal Button previousScreenshotButton { get; private set; }
        [field: SerializeField] internal Button nextScreenshotButton { get; private set; }

        [field: Header("Actions panel")]
        [field: SerializeField] internal Button downloadButton { get; private set; }
        [field: SerializeField] internal Button deleteButton { get; private set; }
        [field: SerializeField] internal Button linkButton { get; private set; }
        [field: SerializeField] internal Button twitterButton { get; private set; }
        [field: SerializeField] internal Button infoButton { get; private set; }
        [field: SerializeField] internal RectTransform infoButtonImageRectTransform { get; private set; }

        private void Awake()
        {
            mainImage.sprite = null;
            mainImageCanvasGroup.alpha = 0;
        }
    }
}
