using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.PhotoDetail;
using DCL.UI;
using DCL.Web3.Identities;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.CameraReelGallery
{
    public class CameraReelController : ISection, IDisposable
    {
        public event Action Activated;
        public readonly CameraReelGalleryController CameraReelGalleryController;

        private readonly CameraReelView view;
        private readonly RectTransform rectTransform;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IMVCManager mvcManager;
        private readonly ICursor cursor;

        private CancellationTokenSource showCancellationTokenSource;

        public CameraReelController(
            CameraReelView view,
            CameraReelGalleryController cameraReelGalleryController,
            ICameraReelStorageService cameraReelStorageService,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            ICursor cursor,
            string storageProgressBarLabelText)
        {
            this.view = view;
            this.cameraReelStorageService = cameraReelStorageService;
            this.web3IdentityCache = web3IdentityCache;
            this.CameraReelGalleryController = cameraReelGalleryController;
            this.mvcManager = mvcManager;
            this.cursor = cursor;

            rectTransform = view.transform.parent.GetComponent<RectTransform>();

            this.view.MouseEnter += StorageFullIconEnter;
            this.view.MouseExit += StorageFullIconExit;
            this.CameraReelGalleryController.ThumbnailClicked += ThumbnailClicked;
            this.CameraReelGalleryController.StorageUpdated += SetStorageStatus;
            this.view.goToCameraButton.onClick.AddListener(OnGoToCameraButtonClicked);

            view.storageProgressBar.SetLabelString(storageProgressBarLabelText);
        }

        private void OnGoToCameraButtonClicked()
        {
            //TODO (Lorenzo): Close gallery and open camera
        }

        private void ThumbnailClicked(List<CameraReelResponseCompact> reels, int index, Action<CameraReelResponseCompact> reelDeleteIntention) =>
            mvcManager.ShowAsync(PhotoDetailController.IssueCommand(new PhotoDetailParameter(reels, index, true, reelDeleteIntention)));

        private void StorageFullIconEnter() =>
            view.storageFullToast.DOFade(1f, view.storageFullToastFadeTime);

        private void StorageFullIconExit() =>
            view.storageFullToast.DOFade(0f, view.storageFullToastFadeTime);

        private async UniTask ShowAsync(CancellationToken ct)
        {
            view.CameraReelGalleryView.gameObject.SetActive(false);

            CameraReelStorageStatus storageStatus = await cameraReelStorageService.GetUserGalleryStorageInfoAsync(web3IdentityCache.Identity.Address, ct);
            SetStorageStatus(storageStatus);

            await CameraReelGalleryController.ShowWalletGalleryAsync(web3IdentityCache.Identity.Address, ct, storageStatus);
        }

        private void SetStorageStatus(CameraReelStorageStatus storageStatus)
        {
            view.storageProgressBar.SetPercentageValue((storageStatus.ScreenshotsAmount * 1.0f / storageStatus.MaxScreenshots) * 100, 0 , storageStatus.MaxScreenshots);
            view.storageFullIcon.SetActive(!storageStatus.HasFreeSpace);

            if (storageStatus.ScreenshotsAmount == 0)
                view.emptyState.SetActive(true);

            view.CameraReelGalleryView.gameObject.SetActive(storageStatus.ScreenshotsAmount > 0);
        }

        public void Activate()
        {
            showCancellationTokenSource = showCancellationTokenSource.SafeRestart();
            view.gameObject.SetActive(true);
            cursor.Unlock();
            ShowAsync(showCancellationTokenSource.Token).SuppressCancellationThrow().Forget();

            Activated?.Invoke();
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
            showCancellationTokenSource.SafeCancelAndDispose();
        }

        public void Animate(int triggerId)
        {
            view.panelAnimator.SetTrigger(triggerId);
            view.headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            view.panelAnimator.Rebind();
            view.headerAnimator.Rebind();
            view.panelAnimator.Update(0);
            view.headerAnimator.Update(0);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void Dispose()
        {
            view.MouseEnter -= StorageFullIconEnter;
            view.MouseExit -= StorageFullIconExit;
            CameraReelGalleryController.Dispose();
            view.goToCameraButton.onClick.RemoveAllListeners();
        }
    }
}
