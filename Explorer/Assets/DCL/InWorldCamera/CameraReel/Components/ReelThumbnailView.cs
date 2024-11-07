using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using DG.Tweening;
using System;
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

        [SerializeField] private LoadingBrightView loadingBrightView;
        [SerializeField] private Image thumbnailImage;

        private CameraReelResponse cameraReelResponse;
        private CancellationTokenSource loadImageCts;
        private ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;

        public void Setup(CameraReelResponse cameraReelData, ICameraReelScreenshotsStorage cameraReelScreenshotsStorageService)
        {
            this.cameraReelResponse = cameraReelData;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorageService;
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

        public void OnPointerEnter(PointerEventData eventData) =>
            transform.DOScale(Vector3.one * 1.03f, 0.3f);

        public void OnPointerExit(PointerEventData eventData) =>
            transform.DOScale(Vector3.one, 0.3f);
    }
}
