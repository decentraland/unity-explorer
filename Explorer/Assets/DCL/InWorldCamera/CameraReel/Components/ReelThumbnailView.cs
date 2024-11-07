using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class ReelThumbnailView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const int THUMBNAIL_WIDTH = 272;
        private const int THUMBNAIL_HEIGHT = 201;

        [Header("References")]
        [SerializeField] private LoadingBrightView loadingBrightView;
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private RectTransform optionButtonContainer;

        [Header("Configuration")]
        [SerializeField] private Vector3 optionButtonOffset = new (-15.83997f, -22f, 0);
        [SerializeField] private float scaleFactorOnHover = 1.03f;
        [SerializeField] private float scaleAnimationDuration = 0.3f;

        private CameraReelResponse cameraReelResponse;
        private CancellationTokenSource loadImageCts;
        private ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private OptionButtonView optionButton;

        public void Setup(CameraReelResponse cameraReelData, ICameraReelScreenshotsStorage cameraReelScreenshotsStorageService, OptionButtonView optionsButton)
        {
            this.cameraReelResponse = cameraReelData;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorageService;
            this.optionButton = optionsButton;

            loadImageCts = loadImageCts.SafeRestart();
            thumbnailImage.sprite = null;
            LoadImage(cameraReelScreenshotsStorage, loadImageCts.Token).Forget();
        }

        private async UniTask LoadImage(ICameraReelScreenshotsStorage cameraReelScreenshotsStorage, CancellationToken token)
        {
            loadingBrightView.StartLoadingAnimation(thumbnailImage.gameObject);
            Texture2D thumbnailTexture = await cameraReelScreenshotsStorage.GetScreenshotThumbnailAsync(cameraReelResponse.thumbnailUrl);
            thumbnailImage.sprite = Sprite.Create(thumbnailTexture, new Rect(0, 0, THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT), Vector2.zero);
            loadingBrightView.FinishLoadingAnimation(thumbnailImage.gameObject);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.DOScale(Vector3.one * scaleFactorOnHover, scaleAnimationDuration);
            optionButton.transform.SetParent(optionButtonContainer.transform);
            optionButton.transform.localPosition = optionButtonOffset;
            optionButton.SetImageData(cameraReelResponse);
            optionButton.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.DOScale(Vector3.one, scaleAnimationDuration);
            optionButton.ResetState();
            optionButton.gameObject.SetActive(false);
        }
    }
}
