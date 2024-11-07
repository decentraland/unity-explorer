using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReel.Components;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using DCL.Web3.Identities;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.CameraReel
{
    public class CameraReelController : ISection, IDisposable
    {
        private const int PAGINATION_LIMIT = 100;
        private const int THUMBNAIL_POOL_DEFAULT_CAPACITY = 100;
        private const int THUMBNAIL_POOL_MAX_SIZE = 500;
        private const int GRID_POOL_DEFAULT_CAPACITY = 10;
        private const int GRID_POOL_MAX_SIZE = 500;

        private readonly CameraReelView view;
        private readonly RectTransform rectTransform;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly PagedCameraReelManager pagedCameraReelManager;
        private readonly ReelGalleryPoolManager reelGalleryPoolManager;
        private readonly List<MonthGridView> monthGridViews = new ();

        private CancellationTokenSource showCancellationTokenSource;

        public CameraReelController(
            CameraReelView view,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.view = view;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.web3IdentityCache = web3IdentityCache;

            pagedCameraReelManager = new PagedCameraReelManager(cameraReelStorageService, web3IdentityCache, PAGINATION_LIMIT);
            reelGalleryPoolManager = new ReelGalleryPoolManager(view.thumbnailViewPrefab, view.monthGridPrefab, view.unusedThumbnailViewObject, view.unusedGridViewObject, THUMBNAIL_POOL_DEFAULT_CAPACITY, THUMBNAIL_POOL_MAX_SIZE, GRID_POOL_DEFAULT_CAPACITY, GRID_POOL_MAX_SIZE);
            rectTransform = view.transform.parent.GetComponent<RectTransform>();


            this.view.OnMouseEnter += OnStorageFullIconEnter;
            this.view.OnMouseExit += OnStorageFullIconExit;
        }

        private void OnStorageFullIconEnter() =>
            view.storageFullToast.DOFade(1f, view.storageFullToastFadeTime);

        private void OnStorageFullIconExit() =>
            view.storageFullToast.DOFade(0f, view.storageFullToastFadeTime);

        private async UniTask OnShow(CancellationToken ct)
        {
            view.emptyState.SetActive(false);
            view.loadingSpinner.SetActive(true);
            view.scrollViewGameObject.SetActive(false);

            CameraReelStorageStatus storageStatus = await cameraReelStorageService.GetUserGalleryStorageInfoAsync(web3IdentityCache.Identity.Address, ct);
            SetStorageStatus(storageStatus);

            if (storageStatus.ScreenshotsAmount == 0)
            {
                view.loadingSpinner.SetActive(false);
                view.emptyState.SetActive(true);
                return;
            }

            await pagedCameraReelManager.Initialize(storageStatus.ScreenshotsAmount, ct);

            for (int i = 0; i < pagedCameraReelManager.GetBucketCount(); i++)
            {
                MonthGridView monthGridView = reelGalleryPoolManager.GetGridElement(view.scrollContentRect);
                var imageBucket = pagedCameraReelManager.GetBucket(i);
                monthGridView.Setup(imageBucket.Item1, imageBucket.Item2, reelGalleryPoolManager, cameraReelScreenshotsStorage, view.optionsButton);
                monthGridViews.Add(monthGridView);
            }

            view.scrollViewGameObject.SetActive(true);
            view.loadingSpinner.SetActive(false);
        }

        private void ReleaseGridViews()
        {
            for (int i = 0; i < monthGridViews.Count; i++)
            {
                monthGridViews[i].Release();
                reelGalleryPoolManager.ReleaseGridElement(monthGridViews[i]);
            }
            monthGridViews.Clear();
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
            pagedCameraReelManager.Flush();
            ReleaseGridViews();
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
            view.optionsButton.Dispose();
        }
    }
}
