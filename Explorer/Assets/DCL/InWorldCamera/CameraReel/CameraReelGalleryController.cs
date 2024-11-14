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
using UnityEditor;
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

        public event Action<CameraReelResponse> ThumbnailClicked;
        public event Action<CameraReelStorageStatus> StorageUpdated;

        private const int THUMBNAIL_POOL_DEFAULT_CAPACITY = 100;
        private const int THUMBNAIL_POOL_MAX_SIZE = 10000;
        private const int GRID_POOL_DEFAULT_CAPACITY = 10;
        private const int GRID_POOL_MAX_SIZE = 500;
        private const int ANIMATION_DELAY = 300;

        private PagedCameraReelManager pagedCameraReelManager;

        private readonly CameraReelGalleryView view;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly IExplorePanelEscapeAction explorePanelEscapeAction;
        private readonly ReelGalleryPoolManager reelGalleryPoolManager;
        private readonly Dictionary<DateTime, MonthGridView> monthViews = new ();
        private readonly Dictionary<MonthGridView, List<ReelThumbnailView>> reelThumbnailViews = new ();
        private readonly Dictionary<CameraReelResponse, Sprite> reelThumbnailCache = new ();
        private readonly OptionButtonController optionButtonController;
        private readonly ContextMenuController contextMenuController;
        private readonly Rect elementMaskRect;

        private bool isLoading = false;
        private bool isDragging = false;
        private float previousY = 1f;
        private CancellationTokenSource loadNextPageCts = new ();
        private ReelThumbnailView[] thumbnailImages;
        private int beginVisible;
        private int endVisible;
        private int currentSize;
        private CameraReelResponse reelToDelete;

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
                view.unusedGridViewObject, THUMBNAIL_POOL_DEFAULT_CAPACITY, THUMBNAIL_POOL_MAX_SIZE, GRID_POOL_DEFAULT_CAPACITY,
                GRID_POOL_MAX_SIZE);


            view.cancelDeleteIntentButton?.onClick.AddListener(() => OnDeletionModalCancelClick());
            view.cancelDeleteIntentBackgroundButton?.onClick.AddListener(() => OnDeletionModalCancelClick(false));
            view.deleteReelButton?.onClick.AddListener(DeleteScreenshot);

            if (this.contextMenuController != null)
            {
                this.contextMenuController.SetPublicRequested += (cameraReelRes, publicFlag) => { };

                this.contextMenuController.ShareToXRequested += cameraReelResponse =>
                {
                    string description = "Check out what I'm doing in Decentraland right now and join me!".Replace(" ", "%20");
                    string url = $"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink)}/{cameraReelResponse.id}";
                    string xUrl = $"https://x.com/intent/post?text={description}&hashtags=DCLCamera&url={url}";

                    EditorGUIUtility.systemCopyBuffer = xUrl;
                    webBrowser.OpenUrl(xUrl);
                };

                this.contextMenuController.CopyPictureLinkRequested += cameraReelResponse =>
                {
                    EditorGUIUtility.systemCopyBuffer = $"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink)}/{cameraReelResponse.id}";
                    ShowSuccessNotification("Link copied!").Forget();
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
                    DeleteScreenshotsAsync(reelToDelete).Forget();
                HideDeleteModal();
            }

            AnimateAndAwaitAsync().Forget();
        }

        private async UniTask DeleteScreenshotsAsync(CameraReelResponse cameraReelResponse, CancellationToken ct = default)
        {
            try
            {
                CameraReelStorageStatus response = await cameraReelStorageService.DeleteScreenshotAsync(cameraReelResponse.id, ct);
                StorageUpdated?.Invoke(response);

                MonthGridView monthGridView = GetMonthGridView(PagedCameraReelManager.GetImageDateTime(cameraReelResponse));
                monthGridView.RemoveThumbnail(cameraReelResponse);

                if (monthGridView.GridIsEmpty())
                    ReleaseGridView(monthGridView);

                if (view.successNotificationView is null) return;

                await ShowSuccessNotification("Photo successfully deleted", ct);
            }
            catch (Exception)
            {
                if (view.errorNotificationView is null) return;

                view.errorNotificationView.Show();
                await UniTask.Delay((int) view.errorSuccessToastDuration * 1000, cancellationToken: ct);
                view.errorNotificationView.Hide();
            }

            reelToDelete = null;
        }

        public async UniTask ShowWalletGallery(string walletAddress, CancellationToken ct, CameraReelStorageStatus? storageStatus = null)
        {
            pagedCameraReelManager = new PagedCameraReelManager(cameraReelStorageService, walletAddress, view.paginationLimit);
            loadNextPageCts = loadNextPageCts.SafeRestart();

            view.scrollRect.verticalNormalizedPosition = 1f;
            previousY = 1f;

            storageStatus ??= await cameraReelStorageService.GetUserGalleryStorageInfoAsync(walletAddress, ct);

            thumbnailImages = new ReelThumbnailView[storageStatus.Value.MaxScreenshots];

            await LoadMorePage(true, ct);

            view.scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
        }

        private async UniTask ShowSuccessNotification(string message, CancellationToken ct = default)
        {
            view.successNotificationView.SetText(message);
            view.successNotificationView.Show();
            await UniTask.Delay((int) view.errorSuccessToastDuration * 1000, cancellationToken: ct);
            view.successNotificationView.Hide();
        }

        private MonthGridView GetMonthGridView(DateTime dateTime)
        {
            MonthGridView monthGridView;
            if (monthViews.TryGetValue(dateTime, out MonthGridView monthView))
                monthGridView = monthView;
            else
            {
                monthGridView = reelGalleryPoolManager.GetGridElement(view.scrollContentRect);
                monthViews.Add(dateTime, monthGridView);
            }

            return monthGridView;
        }

        private async UniTask LoadMorePage(bool firstLoading, CancellationToken ct)
        {
            isLoading = true;
            Dictionary<DateTime, List<CameraReelResponse>> result = await pagedCameraReelManager.FetchNextPage(ct);

            foreach (var bucket in result)
            {
                MonthGridView monthGridView = GetMonthGridView(bucket.Key);

                List<ReelThumbnailView> thumbnailViews = monthGridView.Setup(bucket.Key, bucket.Value, reelGalleryPoolManager, cameraReelScreenshotsStorage, optionButtonController,
                    (cameraReelResponse, sprite) => reelThumbnailCache.Add(cameraReelResponse, sprite),
                    cameraReelResponse => ThumbnailClicked?.Invoke(cameraReelResponse));

                if (reelThumbnailViews.TryGetValue(monthGridView, out List<ReelThumbnailView> thumbnails))
                    thumbnails.AddRange(thumbnailViews);
                else
                    reelThumbnailViews.Add(monthGridView, thumbnailViews);

                for (int i = 0; i < thumbnailViews.Count; i++)
                    thumbnailImages[currentSize + i] = thumbnailViews[i];

                currentSize += thumbnailViews.Count;
            }
            endVisible = currentSize - 1;

            await UniTask.NextFrame(ct);

            if (firstLoading)
                ResetThumbnailsVisibility();
            else
                CheckElementsVisibility(ScrollDirection.UP);

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
            CheckElementsVisibility(value.y > previousY ? ScrollDirection.UP : ScrollDirection.DOWN);
            CheckNeedsToLoadMore();

            previousY = value.y;

            if (endVisible < beginVisible)
                ResetThumbnailsVisibility();
        }

        private void ResetThumbnailsVisibility()
        {
            beginVisible = -1;
            for (int i = 0; i < currentSize; i++)
                if (ViewIntersectsImage(thumbnailImages[i].thumbnailImage))
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
                LoadMorePage(false, loadNextPageCts.Token).Forget();
        }

        private void DisableThumbnailImage(ReelThumbnailView thumbnailView)
        {
            thumbnailView.thumbnailImage.sprite = null;
            thumbnailView.thumbnailImage.enabled = false;
        }

        private void EnableThumbnailImage(ReelThumbnailView thumbnailView)
        {
            if (reelThumbnailCache.TryGetValue(thumbnailView.cameraReelResponse, out Sprite sprite)) thumbnailView.thumbnailImage.sprite = sprite;
            thumbnailView.thumbnailImage.enabled = true;
        }

        private void CheckElementsVisibility(ScrollDirection scrollDirection)
        {
            if (scrollDirection == ScrollDirection.UP)
            {
                int index = beginVisible;

                while (index >= 0 && ViewIntersectsImage(thumbnailImages[index].thumbnailImage))
                {
                    EnableThumbnailImage(thumbnailImages[index]);
                    index--;
                    beginVisible--;
                }
                beginVisible = Mathf.Clamp(beginVisible, 0, currentSize - 1);

                index = endVisible;

                while (index >= 0 && !ViewIntersectsImage(thumbnailImages[index].thumbnailImage))
                {
                    DisableThumbnailImage(thumbnailImages[index]);
                    index--;
                    endVisible--;
                }
                endVisible = Mathf.Clamp(endVisible, 0, currentSize - 1);
            }
            else
            {
                int index = endVisible;

                while (index < currentSize && ViewIntersectsImage(thumbnailImages[index].thumbnailImage))
                {
                    EnableThumbnailImage(thumbnailImages[index]);
                    index++;
                    endVisible++;
                }
                endVisible = Mathf.Clamp(endVisible, 0, currentSize - 1);

                index = beginVisible;

                while (index < currentSize && !ViewIntersectsImage(thumbnailImages[index].thumbnailImage))
                {
                    DisableThumbnailImage(thumbnailImages[index]);
                    index++;
                    beginVisible++;
                }
                beginVisible = Mathf.Clamp(beginVisible, 0, currentSize - 1);
            }
        }

        private bool ViewIntersectsImage(Image image)
        {
            var img = image.rectTransform.GetWorldRect();

            return img.yMax >= elementMaskRect.yMin && img.yMin < elementMaskRect.yMax;
        }

        private void ReleaseGridView(MonthGridView monthGridView)
        {
            monthGridView.Release();
            reelGalleryPoolManager.ReleaseGridElement(monthGridView);
        }

        private void ReleaseGridViews()
        {
            foreach (MonthGridView monthGridView in monthViews.Values)
                ReleaseGridView(monthGridView);
        }

        private void OnDisable()
        {
            ReleaseGridViews();
            reelThumbnailViews.Clear();
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
