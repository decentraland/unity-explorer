using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.ExplorePanel.Components;
using DCL.InWorldCamera.CameraReel.Components;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.CameraReel
{
    public class CameraReelGalleryController : IDisposable
    {
        private enum ScrollDirection
        {
            UP,
            DOWN
        }

        private struct ReelToDeleteInfo
        {
            public readonly string Id;
            public readonly string Datetime;

            public ReelToDeleteInfo(string id, string datetime)
            {
                Id = id;
                Datetime = datetime;
            }
        }

        public event Action<CameraReelResponse> ThumbnailClicked;
        public event Action<CameraReelStorageStatus> StorageUpdated;

        private const int THUMBNAIL_POOL_DEFAULT_CAPACITY = 100;
        private const int THUMBNAIL_POOL_MAX_SIZE = 10000;
        private const int GRID_POOL_DEFAULT_CAPACITY = 10;
        private const int GRID_POOL_MAX_SIZE = 500;
        private const int ANIMATION_DELAY = 300;

        private readonly CameraReelGalleryView view;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly IExplorePanelEscapeAction explorePanelEscapeAction;
        private readonly ReelGalleryPoolManager reelGalleryPoolManager;
        private readonly Dictionary<DateTime, MonthGridController> monthViews = new ();
        private readonly Dictionary<CameraReelResponse, Sprite> reelThumbnailCache = new ();
        private readonly OptionButtonController optionButtonController;
        private readonly ContextMenuController contextMenuController;
        private readonly Rect elementMaskRect;

        private bool isLoading = false;
        private bool isDragging = false;
        private float previousY = 1f;
        private CancellationTokenSource loadNextPageCts = new ();
        private ReelThumbnailController[] thumbnailImages;
        private int beginVisible;
        private int endVisible;
        private int currentSize;
        private CameraReelResponse reelToDelete;
        private PagedCameraReelManager pagedCameraReelManager;

        public CameraReelGalleryController(CameraReelGalleryView view,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            IExplorePanelEscapeAction explorePanelEscapeAction,
            OptionButtonView optionButtonView = null,
            ContextMenuView contextMenuView = null)
        {
            this.view = view;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.explorePanelEscapeAction = explorePanelEscapeAction;
            this.view.Disable += OnDisable;
            this.view.scrollRectDragHandler.BeginDrag += ScrollBeginDrag;
            this.view.scrollRectDragHandler.EndDrag += ScrollEndDrag;
            this.view.scrollBarDragHandler.BeginDrag += ScrollBeginDrag;
            this.view.scrollBarDragHandler.EndDrag += ScrollEndDrag;
            this.elementMaskRect = this.view.elementMask.GetWorldRect();

            if (optionButtonView is not null && contextMenuView is not null)
            {
                this.contextMenuController = new ContextMenuController(contextMenuView);
                this.optionButtonController = new OptionButtonController(optionButtonView, contextMenuController);
            }

            reelGalleryPoolManager = new ReelGalleryPoolManager(view.thumbnailViewPrefab, view.monthGridPrefab, view.unusedThumbnailViewObject,
                view.unusedGridViewObject, cameraReelScreenshotsStorage, THUMBNAIL_POOL_DEFAULT_CAPACITY, THUMBNAIL_POOL_MAX_SIZE, GRID_POOL_DEFAULT_CAPACITY,
                GRID_POOL_MAX_SIZE);

            view.cancelDeleteIntentButton?.onClick.AddListener(() => OnDeletionModalCancelClick());
            view.cancelDeleteIntentBackgroundButton?.onClick.AddListener(() => OnDeletionModalCancelClick(false));
            view.deleteReelButton?.onClick.AddListener(DeleteScreenshot);

            if (this.contextMenuController != null)
            {
                this.contextMenuController.SetPublicRequested += (cameraReelRes, publicFlag) =>
                {
                    async UniTask SetPublicFlagAsync()
                    {
                        try
                        {
                            await this.cameraReelStorageService.UpdateScreenshotVisibilityAsync(cameraReelRes.id, publicFlag);
                            cameraReelRes.isPublic = publicFlag;
                            await ShowSuccessNotificationAsync("Photo successfully updated");
                        }
                        catch (Exception)
                        {
                            await ShowFailureNotificationAsync();
                        }
                    }

                    SetPublicFlagAsync().Forget();
                };

                this.contextMenuController.ShareToXRequested += cameraReelResponse =>
                {
                    string description = "Check out what I'm doing in Decentraland right now and join me!".Replace(" ", "%20");
                    string url = $"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink)}/{cameraReelResponse.id}";
                    string xUrl = $"https://x.com/intent/post?text={description}&hashtags=DCLCamera&url={url}";

                    GUIUtility.systemCopyBuffer = xUrl;
                    webBrowser.OpenUrl(xUrl);
                };

                this.contextMenuController.CopyPictureLinkRequested += cameraReelResponse =>
                {
                    GUIUtility.systemCopyBuffer = $"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink)}/{cameraReelResponse.id}";
                    ShowSuccessNotificationAsync("Link copied!").Forget();
                };

                this.contextMenuController.DownloadRequested += cameraReelResponse => { webBrowser.OpenUrl(cameraReelResponse.url); };

                this.contextMenuController.DeletePictureRequested += cameraReelResponse =>
                {
                    ShowDeleteModal();
                    reelToDelete = cameraReelResponse;
                };
            }
        }

        private void ShowDeleteModal()
        {
            explorePanelEscapeAction.RegisterEscapeAction(HideDeleteModal);
            view.deleteReelModal.gameObject.SetActive(true);
            view.deleteReelModal.DOFade(1f, view.deleteModalAnimationDuration);
        }

        private void HideDeleteModal(InputAction.CallbackContext callbackContext = default)
        {
            explorePanelEscapeAction.RemoveEscapeAction(HideDeleteModal);
            view.deleteReelModal.DOFade(0f, view.deleteModalAnimationDuration).OnComplete(() => view.deleteReelModal.gameObject.SetActive(false));
        }

        private void OnDeletionModalCancelClick(bool waitForAnimation = true)
        {
            async UniTaskVoid AnimateAndAwaitAsync()
            {
                await UniTask.Delay(ANIMATION_DELAY);
                HideDeleteModal();
            }

            if (waitForAnimation)
                AnimateAndAwaitAsync().Forget();
            else
                HideDeleteModal();
        }

        private void DeleteScreenshot()
        {
            async UniTaskVoid AnimateAndAwaitAsync()
            {
                await UniTask.Delay(ANIMATION_DELAY);
                if (reelToDelete is not null)
                    DeleteScreenshotsAsync(new ReelToDeleteInfo(reelToDelete.id, reelToDelete.metadata.dateTime)).Forget();

                reelToDelete = null;
                HideDeleteModal();
            }

            AnimateAndAwaitAsync().Forget();
        }

        private async UniTask DeleteScreenshotsAsync(ReelToDeleteInfo reelToDeleteInfo, CancellationToken ct = default)
        {
            try
            {
                CameraReelStorageStatus response = await cameraReelStorageService.DeleteScreenshotAsync(reelToDeleteInfo.Id, ct);
                StorageUpdated?.Invoke(response);

                int deletedIndex = -1;
                for (int i = beginVisible; i < currentSize; i++)
                    if (deletedIndex >= 0)
                        thumbnailImages[i - 1] = thumbnailImages[i];
                    else if (thumbnailImages[i].CameraReelResponse.id == reelToDeleteInfo.Id)
                        deletedIndex = i;

                thumbnailImages[currentSize - 1] = null;
                currentSize--;
                ResetThumbnailsVisibility();

                MonthGridController monthGridView = GetMonthGrid(PagedCameraReelManager.GetDateTimeFromString(reelToDeleteInfo.Datetime));
                monthGridView.RemoveThumbnail(reelToDeleteInfo.Id);

                if (monthGridView.GridIsEmpty())
                {
                    monthViews.Remove(monthGridView.DateTimeBucket);
                    ReleaseGridView(monthGridView);
                }

                if (view.successNotificationView is null) return;

                await ShowSuccessNotificationAsync("Photo successfully deleted", ct);
            }
            catch (Exception)
            {
                if (view.errorNotificationView is null) return;

                await ShowFailureNotificationAsync(ct);
            }
        }

        public async UniTask ShowWalletGalleryAsync(string walletAddress, CancellationToken ct, CameraReelStorageStatus? storageStatus = null)
        {
            loadNextPageCts = loadNextPageCts.SafeRestart();

            view.scrollRect.verticalNormalizedPosition = 1f;
            previousY = 1f;

            storageStatus ??= await cameraReelStorageService.GetUserGalleryStorageInfoAsync(walletAddress, ct);
            pagedCameraReelManager = new PagedCameraReelManager(cameraReelStorageService, walletAddress, storageStatus.Value.ScreenshotsAmount, view.paginationLimit);
            thumbnailImages = new ReelThumbnailController[storageStatus.Value.MaxScreenshots];

            await LoadMorePageAsync(ct);

            view.scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
        }

        private async UniTask ShowSuccessNotificationAsync(string message, CancellationToken ct = default)
        {
            view.successNotificationView.SetText(message);
            view.successNotificationView.Show();
            await UniTask.Delay((int) view.errorSuccessToastDuration * 1000, cancellationToken: ct);
            view.successNotificationView.Hide();
        }

        private async UniTask ShowFailureNotificationAsync(CancellationToken ct = default)
        {
            view.errorNotificationView.Show();
            await UniTask.Delay((int) view.errorSuccessToastDuration * 1000, cancellationToken: ct);
            view.errorNotificationView.Hide();
        }

        private MonthGridController GetMonthGrid(DateTime dateTime)
        {
            MonthGridController monthGridView;
            if (monthViews.TryGetValue(dateTime, out MonthGridController monthView))
                monthGridView = monthView;
            else
            {
                monthGridView = reelGalleryPoolManager.GetGridElement(view.scrollContentRect);
                monthViews.Add(dateTime, monthGridView);
            }

            return monthGridView;
        }

        private async UniTask LoadMorePageAsync(CancellationToken ct)
        {
            isLoading = true;
            Dictionary<DateTime, List<CameraReelResponse>> result = await pagedCameraReelManager.FetchNextPageAsync(ct);
            float handleHeight = view.verticalScrollbar.handleRect.rect.height;

            foreach (var bucket in result)
            {
                MonthGridController monthGridView = GetMonthGrid(bucket.Key);

                List<ReelThumbnailController> thumbnailViews = monthGridView.Setup(bucket.Key, bucket.Value, optionButtonController,
                    (cameraReelResponse, sprite) => reelThumbnailCache.Add(cameraReelResponse, sprite),
                    cameraReelResponse => ThumbnailClicked?.Invoke(cameraReelResponse));

                for (int i = 0; i < thumbnailViews.Count; i++)
                    thumbnailImages[currentSize + i] = thumbnailViews[i];

                currentSize += thumbnailViews.Count;
            }
            endVisible = currentSize - 1;

            //Wait for layout to update after the addition of new elements
            await UniTask.WaitWhile(() => Mathf.Approximately(view.verticalScrollbar.handleRect.rect.height, handleHeight), cancellationToken: ct);

            HandleElementsVisibility(ScrollDirection.UP);

            previousY = view.scrollRect.verticalNormalizedPosition;
            isLoading = false;
        }

        private void ScrollBeginDrag() => isDragging = true;

        private void ScrollEndDrag()
        {
            isDragging = false;
            CheckNeedsToLoadMore();
        }

        private void OnScrollRectValueChanged(Vector2 value)
        {
            HandleElementsVisibility(value.y > previousY ? ScrollDirection.UP : ScrollDirection.DOWN);
            CheckNeedsToLoadMore();

            previousY = value.y;

            if (endVisible < beginVisible)
                ResetThumbnailsVisibility();
        }

        private void ResetThumbnailsVisibility()
        {
            beginVisible = -1;
            for (int i = 0; i < currentSize; i++)
                if (ViewIntersectsImage(thumbnailImages[i].view.thumbnailImage))
                {
                    if (beginVisible < 0)
                        beginVisible = i;

                    EnableThumbnailImage(thumbnailImages[i]);
                    endVisible = i;
                } else
                    DisableThumbnailImage(thumbnailImages[i]);
        }

        private void CheckNeedsToLoadMore()
        {
            if (currentSize - endVisible < view.loadMoreCounterThreshold && !pagedCameraReelManager.AllImagesLoaded && !isLoading && !isDragging)
                LoadMorePageAsync(loadNextPageCts.Token).Forget();
        }

        private void DisableThumbnailImage(ReelThumbnailController thumbnailController)
        {
            thumbnailController.view.thumbnailImage.sprite = null;
            thumbnailController.view.thumbnailImage.enabled = false;
        }

        private void EnableThumbnailImage(ReelThumbnailController thumbnailController)
        {
            if (reelThumbnailCache.TryGetValue(thumbnailController.CameraReelResponse, out Sprite sprite))
                thumbnailController.view.thumbnailImage.sprite = sprite;
            thumbnailController.view.thumbnailImage.enabled = true;
        }

        private void HandleElementsVisibility(ScrollDirection scrollDirection)
        {
            if (scrollDirection == ScrollDirection.UP)
            {
                while (beginVisible >= 0 && ViewIntersectsImage(thumbnailImages[beginVisible].view.thumbnailImage))
                    beginVisible--;

                beginVisible++;
                beginVisible = Mathf.Clamp(beginVisible, 0, currentSize - 1);

                while (endVisible >= 0 && !ViewIntersectsImage(thumbnailImages[endVisible].view.thumbnailImage))
                    endVisible--;
                endVisible = Mathf.Clamp(endVisible, 0, currentSize - 1);
            }
            else
            {
                while (endVisible < currentSize && ViewIntersectsImage(thumbnailImages[endVisible].view.thumbnailImage))
                    endVisible++;

                endVisible--;
                endVisible = Mathf.Clamp(endVisible, 0, currentSize - 1);

                while (beginVisible < currentSize && !ViewIntersectsImage(thumbnailImages[beginVisible].view.thumbnailImage))
                    beginVisible++;
                beginVisible = Mathf.Clamp(beginVisible, 0, currentSize - 1);
            }

            for (int i = 0; i < beginVisible; i++)
                if (thumbnailImages[i].view.thumbnailImage.enabled)
                    DisableThumbnailImage(thumbnailImages[i]);
            for (int i = beginVisible; i <= endVisible; i++)
                if (!thumbnailImages[i].view.thumbnailImage.enabled)
                    EnableThumbnailImage(thumbnailImages[i]);
            for (int i = endVisible + 1; i < currentSize; i++)
                if (thumbnailImages[i].view.thumbnailImage.enabled)
                    DisableThumbnailImage(thumbnailImages[i]);
        }

        private bool ViewIntersectsImage(Image image)
        {
            var img = image.rectTransform.GetWorldRect();

            return img.yMax >= elementMaskRect.yMin && img.yMin < elementMaskRect.yMax;
        }

        private void ReleaseGridView(MonthGridController monthGridView)
        {
            monthGridView.Release();
            reelGalleryPoolManager.ReleaseGridElement(monthGridView);
        }

        private void ReleaseGridViews()
        {
            foreach (MonthGridController monthGridView in monthViews.Values)
                ReleaseGridView(monthGridView);
        }

        private void OnDisable()
        {
            ReleaseGridViews();
            monthViews.Clear();
            reelThumbnailCache.Clear();
            beginVisible = 0;
            endVisible = 0;
            currentSize = 0;
            thumbnailImages = null;
            view.scrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);

            loadNextPageCts.SafeCancelAndDispose();
        }

        public void Dispose()
        {
            OnDisable();
            view.Disable -= OnDisable;
            ThumbnailClicked = null;
            StorageUpdated = null;
            view.cancelDeleteIntentButton?.onClick.RemoveAllListeners();
            view.cancelDeleteIntentBackgroundButton?.onClick.RemoveAllListeners();
            explorePanelEscapeAction.RemoveEscapeAction(HideDeleteModal);

            if (optionButtonController is not null)
                optionButtonController.Dispose();
        }
    }
}
