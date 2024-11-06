using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReel.Components;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using DCL.Web3.Identities;
using DG.Tweening;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.CameraReel
{
    public class CameraReelController : ISection, IDisposable
    {
        private readonly int PAGINATION_LIMIT = 100;

        private readonly CameraReelView view;
        private readonly RectTransform rectTransform;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly PagedCameraReelManager pagedCameraReelManager;
        private readonly ReelThumbnailPoolManager reelThumbnailPoolManager;

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
            reelThumbnailPoolManager = new ReelThumbnailPoolManager(view.thumbnailViewPrefab, view.unusedThumbnailViewObject, 100, 500);
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.view.loopList.InitListView(0, OnGetItemByIndex);


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
            view.loopList.gameObject.SetActive(false);

            CameraReelStorageStatus storageStatus = await cameraReelStorageService.GetUserGalleryStorageInfoAsync(web3IdentityCache.Identity.Address, ct);
            SetStorageStatus(storageStatus);

            if (storageStatus.ScreenshotsAmount == 0)
            {
                view.loadingSpinner.SetActive(false);
                view.emptyState.SetActive(true);
                return;
            }

            await pagedCameraReelManager.Initialize(ct);

            view.loopList.gameObject.SetActive(true);
            view.loopList.SetListItemCount(pagedCameraReelManager.GetBucketCount(), false);
            view.loadingSpinner.SetActive(false);
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            MonthGridView gridView = listItem.GetComponent<MonthGridView>();
            var imageBucket = pagedCameraReelManager.GetBucket(index);

            gridView.Setup(imageBucket.Item1, imageBucket.Item2, reelThumbnailPoolManager, cameraReelScreenshotsStorage);

            return listItem;
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
            view.loopList.SetListItemCount(0, false);
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
        }
    }
}
