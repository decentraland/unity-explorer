using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using DCL.Web3.Identities;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.CameraReel
{
    public class CameraReelController : ISection, IDisposable
    {
        private readonly CameraReelView view;
        private readonly RectTransform rectTransform;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly CameraReelGalleryController cameraReelGalleryController;

        private CancellationTokenSource showCancellationTokenSource;

        public CameraReelController(
            CameraReelView view,
            CameraReelGalleryController cameraReelGalleryController,
            ICameraReelStorageService cameraReelStorageService,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.view = view;
            this.cameraReelStorageService = cameraReelStorageService;
            this.web3IdentityCache = web3IdentityCache;
            this.cameraReelGalleryController = cameraReelGalleryController;

            rectTransform = view.transform.parent.GetComponent<RectTransform>();

            this.view.OnMouseEnter += OnStorageFullIconEnter;
            this.view.OnMouseExit += OnStorageFullIconExit;
            this.cameraReelGalleryController.ThumbnailClicked += ThumbnailClicked;
            this.cameraReelGalleryController.StorageUpdated += SetStorageStatus;
        }

        private void ThumbnailClicked(CameraReelResponse cameraReelResponse)
        {
            Debug.Log($"OnThumbnailClicked: {cameraReelResponse.id}");
        }

        private void OnStorageFullIconEnter() =>
            view.storageFullToast.DOFade(1f, view.storageFullToastFadeTime);

        private void OnStorageFullIconExit() =>
            view.storageFullToast.DOFade(0f, view.storageFullToastFadeTime);

        private async UniTask OnShow(CancellationToken ct)
        {
            view.emptyState.SetActive(false);
            view.loadingSpinner.SetActive(true);
            view.cameraReelGalleryView.gameObject.SetActive(false);

            CameraReelStorageStatus storageStatus = await cameraReelStorageService.GetUserGalleryStorageInfoAsync(web3IdentityCache.Identity.Address, ct);
            SetStorageStatus(storageStatus);

            if (storageStatus.ScreenshotsAmount == 0)
            {
                view.loadingSpinner.SetActive(false);
                view.emptyState.SetActive(true);
                return;
            }

            await cameraReelGalleryController.ShowWalletGallery(web3IdentityCache.Identity.Address, ct, storageStatus);

            view.cameraReelGalleryView.gameObject.SetActive(true);
            view.loadingSpinner.SetActive(false);
        }

        private void SetStorageStatus(CameraReelStorageStatus storageStatus)
        {
            view.storageProgressBar.MaxRealValue = storageStatus.MaxScreenshots;
            view.storageProgressBar.MinRealValue = 0;
            view.storageProgressBar.SetPercentageValue((storageStatus.ScreenshotsAmount * 1.0f / storageStatus.MaxScreenshots) * 100);
            view.storageFullIcon.SetActive(!storageStatus.HasFreeSpace);
        }

        public void Activate()
        {
            showCancellationTokenSource = showCancellationTokenSource.SafeRestart();
            view.gameObject.SetActive(true);
            OnShow(showCancellationTokenSource.Token).Forget();
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
            showCancellationTokenSource.SafeCancelAndDispose();
        }

        public void Animate(int triggerId)
        {

        }

        public void ResetAnimator()
        {

        }

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void Dispose()
        {
            view.OnMouseEnter -= OnStorageFullIconEnter;
            view.OnMouseExit -= OnStorageFullIconExit;
            cameraReelGalleryController.Dispose();
        }
    }
}
