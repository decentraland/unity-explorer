using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class ReelThumbnailController : IDisposable
    {
        internal readonly ReelThumbnailView view;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;

        private OptionButtonController optionButton;
        private CancellationTokenSource loadImageCts;

        public event Action<CameraReelResponseCompact, Sprite> ThumbnailLoaded;
        public event Action<CameraReelResponseCompact> ThumbnailClicked;

        public CameraReelResponseCompact CameraReelResponse { get; private set; }

        public ReelThumbnailController(ReelThumbnailView view,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorageService)
        {
            this.view = view;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorageService;
            this.view.PointerEnter += OnPointerEnter;
            this.view.PointerExit += OnPointerExit;
        }

        public void Setup(CameraReelResponseCompact cameraReelData, OptionButtonController optionsButton)
        {
            this.CameraReelResponse = cameraReelData;
            this.optionButton = optionsButton;

            if (this.optionButton is not null)
                this.optionButton.Hide += ToNormalAnimation;

            loadImageCts = loadImageCts.SafeRestart();
            view.thumbnailImage.sprite = null;
            LoadImageAsync(loadImageCts.Token).Forget();
        }

        private async UniTask LoadImageAsync(CancellationToken token)
        {
            view.loadingBrightView.StartLoadingAnimation(view.thumbnailImage.gameObject);

            Texture2D thumbnailTexture = await cameraReelScreenshotsStorage.GetScreenshotThumbnailAsync(CameraReelResponse.thumbnailUrl, token);
            view.thumbnailImage.sprite = Sprite.Create(thumbnailTexture, new Rect(0, 0, thumbnailTexture.width, thumbnailTexture.height), Vector2.zero);

            view.loadingBrightView.FinishLoadingAnimation(view.thumbnailImage.gameObject);

            view.thumbnailImage.DOFade(1f, view.thumbnailLoadedAnimationDuration).ToUniTask(cancellationToken: token).Forget();

            ThumbnailLoaded?.Invoke(CameraReelResponse, view.thumbnailImage.sprite);
            view.button.onClick.AddListener( () => ThumbnailClicked?.Invoke(CameraReelResponse));
        }

        public void Dispose()
        {
            ThumbnailLoaded = null;
            ThumbnailClicked = null;
            view.button.onClick.RemoveAllListeners();
            optionButton.Hide -= ToNormalAnimation;
            loadImageCts.SafeCancelAndDispose();
        }

        public void OnPoolGet()
        {
            view.gameObject.SetActive(true);
            view.thumbnailImage.enabled = true;
            view.thumbnailImage.sprite = null;
        }

        public void OnPoolRelease(Transform parent)
        {
            view.transform.SetParent(parent, false);
            view.gameObject.SetActive(false);
            loadImageCts.SafeCancelAndDispose();
        }

        private void ToNormalAnimation()
        {
            view.transform.DOScale(Vector3.one, view.scaleAnimationDuration);
            view.outline.SetActive(false);
        }

        public void Release()
        {
            ThumbnailLoaded = null;
            ThumbnailClicked = null;
            view.button.onClick.RemoveAllListeners();
            view.outline.SetActive(false);
            optionButton.Hide -= ToNormalAnimation;
            loadImageCts.SafeCancelAndDispose();
        }

        private void OnPointerEnter()
        {
            view.transform.DOScale(Vector3.one * view.scaleFactorOnHover, view.scaleAnimationDuration);
            optionButton?.Show(CameraReelResponse, view.optionButtonContainer.transform, view.optionButtonOffset);
            view.outline.SetActive(true);
        }

        private void OnPointerExit()
        {
            if (optionButton != null)
            {
                if (optionButton.IsContextMenuOpen()) return;

                optionButton.HideControl();
                ToNormalAnimation();
            }
            else
                ToNormalAnimation();
        }
    }
}
