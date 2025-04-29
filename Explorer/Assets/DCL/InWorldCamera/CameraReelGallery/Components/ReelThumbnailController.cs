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
        private readonly RectTransform rectTransform;

        private CameraReelOptionButtonController? optionButton;
        private CancellationTokenSource loadImageCts;
        private bool imageLoaded;

        public event Action<CameraReelResponseCompact, Texture>? ThumbnailLoaded;
        public event Action<CameraReelResponseCompact>? ThumbnailClicked;

        public CameraReelResponseCompact CameraReelResponse { get; private set; }

        public ReelThumbnailController(ReelThumbnailView view,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorageService)
        {
            this.view = view;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorageService;
            this.rectTransform = view.GetComponent<RectTransform>();
            this.view.PointerEnter += PointerEnter;
            this.view.PointerExit += PointerExit;
        }

        public void Setup(CameraReelResponseCompact cameraReelData, CameraReelOptionButtonController? optionsButton)
        {
            this.CameraReelResponse = cameraReelData;
            this.optionButton = optionsButton;
            imageLoaded = false;

            if (this.optionButton is not null)
                this.optionButton.Hide += ToNormalAnimation;

            loadImageCts = loadImageCts.SafeRestart();
            view.thumbnailImage.texture = null;
            LoadImageAsync(loadImageCts.Token).Forget();
        }

        private async UniTaskVoid LoadImageAsync(CancellationToken token)
        {
            view.loadingBrightView.StartLoadingAnimation(view.thumbnailImage.gameObject);

            Texture2D thumbnailTexture = await cameraReelScreenshotsStorage.GetScreenshotThumbnailAsync(CameraReelResponse.thumbnailUrl, token);
            thumbnailTexture.Apply(false, true);
            float originalToSmallerRatio = thumbnailTexture.height * 1f / rectTransform.rect.height;
            float realWidth = originalToSmallerRatio * rectTransform.rect.width;
            float realWidthDiff = thumbnailTexture.width - realWidth;
            view.thumbnailImage.texture = thumbnailTexture;
            view.thumbnailImage.uvRect = new Rect((realWidthDiff / 2f) / thumbnailTexture.width, 0, (thumbnailTexture.width - realWidthDiff) / thumbnailTexture.width, 1);

            view.loadingBrightView.FinishLoadingAnimation(view.thumbnailImage.gameObject);

            view.thumbnailImage.DOFade(1f, view.thumbnailLoadedAnimationDuration).ToUniTask(cancellationToken: token).Forget();

            ThumbnailLoaded?.Invoke(CameraReelResponse, thumbnailTexture);
            view.button.onClick.AddListener( () => ThumbnailClicked?.Invoke(CameraReelResponse));
            imageLoaded = true;
        }

        public void Dispose()
        {
            ThumbnailLoaded = null;
            ThumbnailClicked = null;
            view.button.onClick.RemoveAllListeners();
            if (optionButton != null)
                optionButton.Hide -= ToNormalAnimation;
            loadImageCts.SafeCancelAndDispose();
        }

        public void PoolGet()
        {
            view.gameObject.SetActive(true);
            view.thumbnailImage.enabled = true;
        }

        public void PoolRelease(Transform parent)
        {
            view.thumbnailImage.texture = null;
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
            if (optionButton != null)
                optionButton.Hide -= ToNormalAnimation;
            loadImageCts.SafeCancelAndDispose();
        }

        private void PointerEnter()
        {
            if (!imageLoaded) return;

            view.transform.DOScale(Vector3.one * view.scaleFactorOnHover, view.scaleAnimationDuration);
            optionButton?.Show(CameraReelResponse, view.optionButtonContainer.transform, view.optionButtonOffset);
            view.outline.SetActive(true);
        }

        private void PointerExit()
        {
            if (!imageLoaded) return;

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
