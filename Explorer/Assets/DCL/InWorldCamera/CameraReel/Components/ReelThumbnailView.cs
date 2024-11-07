using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class ReelThumbnailView : MonoBehaviour
    {
        private const int THUMBNAIL_WIDTH = 272;
        private const int THUMBNAIL_HEIGHT = 201;

        [SerializeField] private LoadingBrightView loadingBrightView;
        [SerializeField] private Image thumbnailImage;

        private CameraReelResponse cameraReelResponse;
        private CancellationTokenSource loadImageCts;

        public void Setup(CameraReelResponse cameraReelData, ICameraReelScreenshotsStorage cameraReelScreenshotsStorage)
        {
            this.cameraReelResponse = cameraReelData;
            loadImageCts = loadImageCts.SafeRestart();
            LoadImage(cameraReelScreenshotsStorage, loadImageCts.Token).Forget();
        }

        public void Reset()
        {
            thumbnailImage.gameObject.SetActive(false);
        }

        private async UniTask LoadImage(ICameraReelScreenshotsStorage cameraReelScreenshotsStorage, CancellationToken token)
        {
            loadingBrightView.StartLoadingAnimation(thumbnailImage.gameObject);
            Texture2D thumbnailTexture = await cameraReelScreenshotsStorage.GetScreenshotThumbnailAsync(cameraReelResponse.thumbnailUrl);
            thumbnailImage.sprite = Sprite.Create(thumbnailTexture, new Rect(0, 0, THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT), Vector2.zero);
            loadingBrightView.FinishLoadingAnimation(thumbnailImage.gameObject);
        }

        private void OnDisable() =>
            loadImageCts.SafeCancelAndDispose();

    }
}
