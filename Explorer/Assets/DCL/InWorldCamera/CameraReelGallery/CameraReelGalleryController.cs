using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.ExplorePanel.Components;
using DCL.InWorldCamera.CameraReelGallery.Components;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.CameraReelToast;
using DCL.InWorldCamera.ReelActions;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.WebRequests;
using DCL.UI.Utilities;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.CameraReelGallery
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

        public event Action<List<CameraReelResponseCompact>, int, Action<CameraReelResponseCompact>>? ThumbnailClicked;
        public event Action<CameraReelStorageStatus>? StorageUpdated;
        public event Action ScreenshotDeleted;
        public event Action ScreenshotShared;
        public event Action ScreenshotDownloaded;
        public event Action<int> MaxThumbnailsUpdated;

        private const int THUMBNAIL_POOL_DEFAULT_CAPACITY = 100;
        private const int THUMBNAIL_POOL_MAX_SIZE = 10000;
        private const int GRID_POOL_DEFAULT_CAPACITY = 10;
        private const int GRID_POOL_MAX_SIZE = 500;
        private const int ANIMATION_DELAY = 300;

        private static readonly ListObjectPool<CameraReelResponseCompact> CAMERA_REEL_RESPONSES_POOL = new ();

        private readonly CameraReelGalleryView view;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly IExplorePanelEscapeAction? explorePanelEscapeAction;
        private readonly IDecentralandUrlsSource? decentralandUrlsSource;
        private readonly ISystemClipboard? systemClipboard;
        private readonly IWebBrowser? webBrowser;
        private readonly ReelGalleryPoolManager reelGalleryPoolManager;
        private readonly Dictionary<DateTime, MonthGridController> monthViews = new ();
        private readonly Dictionary<CameraReelResponseCompact, Texture> reelThumbnailCache = new ();
        private readonly CameraReelOptionButtonController? optionButtonController;
        private readonly Rect elementMaskRect;
        private readonly ReelGalleryStringMessages? reelGalleryStringMessages;
        private readonly ReelGalleryConfigParams reelGalleryConfigParams;
        private readonly bool useSignedRequest;
        private readonly IWebRequestController webRequestController;

        private bool isLoading;
        private bool isDragging;
        private float previousY = 1f;
        private CancellationTokenSource loadNextPageCts = new ();
        private CancellationTokenSource setPublicCts = new ();
        private CancellationTokenSource deleteScreenshotCts = new ();
        private CancellationTokenSource downloadScreenshotCts = new ();
        private ReelThumbnailController[] thumbnailImages;
        private int beginVisible;
        private int endVisible;
        private int currentSize;
        private CameraReelResponseCompact reelToDelete;
        private PagedCameraReelManager pagedCameraReelManager;

        public CameraReelGalleryController(CameraReelGalleryView view,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ReelGalleryConfigParams reelGalleryConfigParams,
            bool useSignedRequest,
            IWebRequestController webRequestController,
            CameraReelOptionButtonView? optionButtonView = null,
            IWebBrowser? webBrowser = null,
            IDecentralandUrlsSource? decentralandUrlsSource = null,
            IExplorePanelEscapeAction? explorePanelEscapeAction = null,
            ISystemClipboard? systemClipboard = null,
            ReelGalleryStringMessages? reelGalleryStringMessages = null,
            IMVCManager? mvcManager = null)
        {
            this.view = view;
            this.cameraReelStorageService = cameraReelStorageService;
            this.explorePanelEscapeAction = explorePanelEscapeAction;
            this.reelGalleryConfigParams = reelGalleryConfigParams;
            this.reelGalleryStringMessages = reelGalleryStringMessages;
            this.view.Disable += OnDisable;
            this.view.scrollRectDragHandler.BeginDrag += ScrollBeginDrag;
            this.view.scrollRectDragHandler.EndDrag += ScrollEndDrag;
            this.view.scrollBarDragHandler.BeginDrag += ScrollBeginDrag;
            this.view.scrollBarDragHandler.EndDrag += ScrollEndDrag;
            this.elementMaskRect = this.view.elementMask.GetWorldRect();
            this.useSignedRequest = useSignedRequest;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.systemClipboard = systemClipboard;
            this.webBrowser = webBrowser;

            this.view.scrollRect.SetScrollSensitivityBasedOnPlatform();

            if (optionButtonView is not null)
                this.optionButtonController = new CameraReelOptionButtonController(optionButtonView, mvcManager!);

            reelGalleryPoolManager = new ReelGalleryPoolManager(view.thumbnailViewPrefab, view.monthGridPrefab, view.unusedThumbnailViewObject,
                view.unusedGridViewObject, cameraReelScreenshotsStorage,
                reelGalleryConfigParams,
                THUMBNAIL_POOL_DEFAULT_CAPACITY, THUMBNAIL_POOL_MAX_SIZE, GRID_POOL_DEFAULT_CAPACITY, GRID_POOL_MAX_SIZE);

            view.cancelDeleteIntentButton?.onClick.AddListener(() => OnDeletionModalCancelClick());
            view.cancelDeleteIntentBackgroundButton?.onClick.AddListener(() => OnDeletionModalCancelClick(false));
            view.deleteReelButton?.onClick.AddListener(DeleteScreenshot);

            if (this.optionButtonController != null)
            {
                this.optionButtonController.SetPublicRequested += SetReelPublic;
                this.optionButtonController.ShareToXRequested += ShareToX;
                this.optionButtonController.CopyPictureLinkRequested += CopyPictureLink;
                this.optionButtonController.DownloadRequested += DownloadReelLocally;
                this.optionButtonController.DeletePictureRequested += DeleteReel;
            }
        }

        private void DeleteReel(CameraReelResponseCompact response)
        {
            ShowDeleteModal();
            reelToDelete = response;
        }

        private void DownloadReelLocally(CameraReelResponseCompact response)
        {
            async UniTaskVoid DownloadAndOpenAsync(CancellationToken ct)
            {
                try
                {
                    await ReelCommonActions.DownloadReelToFileAsync(webRequestController, new Uri(response.url), ct);
                    ScreenshotDownloaded?.Invoke();

                    view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.DOWNLOAD,
                        reelGalleryStringMessages?.PhotoSuccessfullyDownloadedMessage);
                }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL));
                    view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.FAILURE);
                }
            }

            downloadScreenshotCts = downloadScreenshotCts.SafeRestart();
            DownloadAndOpenAsync(downloadScreenshotCts.Token).Forget();
        }

        private void CopyPictureLink(CameraReelResponseCompact response)
        {
            ReelCommonActions.CopyReelLink(response.id, decentralandUrlsSource!, systemClipboard!);
            view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.SUCCESS, reelGalleryStringMessages?.LinkCopiedMessage);
        }

        private void ShareToX(CameraReelResponseCompact response)
        {
            ReelCommonActions.ShareReelToX(reelGalleryStringMessages?.ShareToXMessage, response.id, decentralandUrlsSource!, systemClipboard!, webBrowser!);
            ScreenshotShared?.Invoke();
        }

        private void SetReelPublic(CameraReelResponseCompact response, bool isPublic)
        {
            async UniTaskVoid SetPublicFlagAsync(CancellationToken ct)
            {
                try
                {
                    await this.cameraReelStorageService.UpdateScreenshotVisibilityAsync(response.id, isPublic, ct);
                    response.isPublic = isPublic;
                    view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.SUCCESS, reelGalleryStringMessages?.PhotoSuccessfullyUpdatedMessage);
                }
                catch (WebRequestException e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL));
                    view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.FAILURE);
                }
            }

            SetPublicFlagAsync(setPublicCts.Token).Forget();
        }

        private void ShowDeleteModal()
        {
            explorePanelEscapeAction?.RegisterEscapeAction(HideDeleteModal);
            view.deleteReelModal.gameObject.SetActive(true);
            view.deleteReelModal.DOFade(1f, view.deleteModalAnimationDuration);
        }

        private void HideDeleteModal(InputAction.CallbackContext callbackContext = default)
        {
            explorePanelEscapeAction?.RemoveEscapeAction(HideDeleteModal);
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
                    DeleteScreenshotsAsync(new ReelToDeleteInfo(reelToDelete.id, reelToDelete.dateTime), deleteScreenshotCts.Token).Forget();

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

                int deletedIndex = -1;
                for (int i = 0; i < currentSize; i++)
                    if (deletedIndex >= 0)
                        thumbnailImages[i - 1] = thumbnailImages[i];
                    else if (thumbnailImages[i].CameraReelResponse.id == reelToDeleteInfo.Id)
                        deletedIndex = i;

                thumbnailImages[currentSize - 1] = null;
                currentSize--;
                ResetThumbnailsVisibility();

                MonthGridController monthGridView = GetMonthGrid(ReelUtility.GetMonthDateTimeFromString(reelToDeleteInfo.Datetime));
                monthGridView.RemoveThumbnail(reelToDeleteInfo.Id);

                if (monthGridView.GridIsEmpty())
                {
                    monthViews.Remove(monthGridView.DateTimeBucket);
                    ReleaseGridView(monthGridView);
                }

                pagedCameraReelManager.RemoveReelId(reelToDeleteInfo.Id);

                ScreenshotDeleted?.Invoke();
                StorageUpdated?.Invoke(response);

                view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.SUCCESS, reelGalleryStringMessages?.PhotoSuccessfullyDeletedMessage);
            }
            catch (WebRequestException e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL));

                view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.FAILURE);
            }
        }

        private void PrepareShowGallery(CancellationToken ct)
        {
            view.loadingSpinner.SetActive(true);
            view.emptyState.SetActive(false);
            loadNextPageCts = loadNextPageCts.SafeRestartLinked(ct);
            setPublicCts = setPublicCts.SafeRestart();
            deleteScreenshotCts = deleteScreenshotCts.SafeRestart();

            view.scrollRect.verticalNormalizedPosition = 1f;
            previousY = 1f;
        }

        private void FinishShowGallery()
        {
            view.scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            view.loadingSpinner.SetActive(false);
        }

        public async UniTask ShowWalletGalleryAsync(string walletAddress, CancellationToken ct, CameraReelStorageStatus? storageStatus = null)
        {
            PrepareShowGallery(ct);

            storageStatus ??= await cameraReelStorageService.UnsignedGetUserGalleryStorageInfoAsync(walletAddress, ct);

            if (storageStatus.Value.ScreenshotsAmount == 0)
            {
                view.emptyState.SetActive(true);
                FinishShowGallery();
                return;
            }

            pagedCameraReelManager = new PagedCameraReelManager(cameraReelStorageService, new PagedCameraReelManagerParameters(walletAddress, useSignedRequest), storageStatus.Value.ScreenshotsAmount, view.PaginationLimit);
            thumbnailImages = new ReelThumbnailController[storageStatus.Value.MaxScreenshots];

            await LoadMorePageAsync(ct);

            FinishShowGallery();
        }

        public async UniTask ShowPlaceGalleryAsync(string placeId, CancellationToken ct)
        {
            PrepareShowGallery(ct);

            CameraReelStorageStatus storageStatus = await cameraReelStorageService.GetPlaceGalleryStorageInfoAsync(placeId, ct);
            pagedCameraReelManager = new PagedCameraReelManager(cameraReelStorageService, new PagedCameraReelManagerParameters(placeId), storageStatus.ScreenshotsAmount, view.PaginationLimit);
            thumbnailImages = new ReelThumbnailController[storageStatus.MaxScreenshots];

            await LoadMorePageAsync(ct);

            FinishShowGallery();
        }

        private MonthGridController GetMonthGrid(DateTime dateTime)
        {
            MonthGridController monthGridView = null;

            if (!reelGalleryConfigParams.GroupByMonth && monthViews.Count == 1)
                foreach (MonthGridController controller in monthViews.Values)
                    monthGridView = controller;
            else if (monthViews.TryGetValue(dateTime, out MonthGridController monthView))
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
            Dictionary<DateTime, List<CameraReelResponseCompact>> result = await pagedCameraReelManager.FetchNextPageAsync(CAMERA_REEL_RESPONSES_POOL, ct);

            foreach (var bucket in result)
            {
                MonthGridController monthGridView = GetMonthGrid(bucket.Key);

                IReadOnlyList<ReelThumbnailController> thumbnailViews = monthGridView.Setup(bucket.Key, bucket.Value, optionButtonController,
                    (cameraReelResponse, sprite) => reelThumbnailCache.Add(cameraReelResponse, sprite),
                    cameraReelResponse =>
                        ThumbnailClicked?.Invoke(pagedCameraReelManager.AllOrderedResponses, pagedCameraReelManager.AllOrderedResponses.IndexOf(cameraReelResponse), compact =>
                        {
                            reelToDelete = compact;
                            DeleteScreenshot();
                        })
                    );

                for (int i = 0; i < thumbnailViews.Count; i++)
                    thumbnailImages[currentSize + i] = thumbnailViews[i];

                currentSize += thumbnailViews.Count;
                CAMERA_REEL_RESPONSES_POOL.Release(bucket.Value);
            }

            MaxThumbnailsUpdated?.Invoke(currentSize);

            DictionaryPool<DateTime, List<CameraReelResponseCompact>>.Release(result);
            endVisible = currentSize - 1;

            //ScrollRect gets updated in LateUpdate, therefore waiting for PostLateUpdate ensures that the layout has been correctly updated
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

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
            //Exclude visibility computation when scrolling over the top or bottom of the scroll rect due to elasticity
            if (view.scrollRect is { verticalNormalizedPosition: >= 1f, velocity: { y: > 0f } } or { verticalNormalizedPosition: <= 0f, velocity: { y: < 0f } })
                return;

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

            //If no image is visible (achieved while scrolling against the scroll limit), set beginVisible to 0
            if (beginVisible < 0)
                beginVisible = 0;
        }

        private void CheckNeedsToLoadMore()
        {
            if (currentSize - endVisible < view.loadMoreCounterThreshold && !pagedCameraReelManager.AllImagesLoaded && !isLoading && !isDragging)
                LoadMorePageAsync(loadNextPageCts.Token).Forget();
        }

        private void DisableThumbnailImage(ReelThumbnailController thumbnailController)
        {
            thumbnailController.view.thumbnailImage.texture = null;
            thumbnailController.view.thumbnailImage.enabled = false;
        }

        private void EnableThumbnailImage(ReelThumbnailController thumbnailController)
        {
            if (reelThumbnailCache.TryGetValue(thumbnailController.CameraReelResponse, out Texture sprite))
                thumbnailController.view.thumbnailImage.texture = sprite;
            thumbnailController.view.thumbnailImage.enabled = true;
        }

        private void HandleElementsVisibility(ScrollDirection scrollDirection)
        {
            if (currentSize == 0) return;

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

        private bool ViewIntersectsImage(RawImage image)
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

            foreach (KeyValuePair<CameraReelResponseCompact, Texture> row in reelThumbnailCache)
                GameObject.Destroy(row.Value);

            reelThumbnailCache.Clear();
            beginVisible = 0;
            endVisible = 0;
            currentSize = 0;
            thumbnailImages = null;
            view.scrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);

            loadNextPageCts.SafeCancelAndDispose();
            setPublicCts.SafeCancelAndDispose();
            deleteScreenshotCts.SafeCancelAndDispose();

            HideDeleteModal();
            optionButtonController?.HideControl();
        }

        public void Dispose()
        {
            OnDisable();
            view.Disable -= OnDisable;
            ThumbnailClicked = null;
            StorageUpdated = null;
            ScreenshotDeleted = null;
            view.cancelDeleteIntentButton?.onClick.RemoveAllListeners();
            view.cancelDeleteIntentBackgroundButton?.onClick.RemoveAllListeners();
            explorePanelEscapeAction?.RemoveEscapeAction(HideDeleteModal);
            downloadScreenshotCts.SafeCancelAndDispose();

            optionButtonController?.Dispose();

            if (this.optionButtonController != null)
            {
                this.optionButtonController.SetPublicRequested -= SetReelPublic;
                this.optionButtonController.ShareToXRequested -= ShareToX;
                this.optionButtonController.CopyPictureLinkRequested -= CopyPictureLink;
                this.optionButtonController.DownloadRequested -= DownloadReelLocally;
                this.optionButtonController.DeletePictureRequested -= DeleteReel;
            }
        }
    }

    public struct ReelGalleryStringMessages
    {
        public readonly string? ShareToXMessage;
        public readonly string? PhotoSuccessfullyDeletedMessage;
        public readonly string? PhotoSuccessfullyUpdatedMessage;
        public readonly string? PhotoSuccessfullyDownloadedMessage;
        public readonly string? LinkCopiedMessage;

        public ReelGalleryStringMessages(string? shareToXMessage, string? photoSuccessfullyDeletedMessage, string? photoSuccessfullyUpdatedMessage, string? photoSuccessfullyDownloadedMessage, string? linkCopiedMessage)
        {
            ShareToXMessage = shareToXMessage;
            PhotoSuccessfullyDeletedMessage = photoSuccessfullyDeletedMessage;
            PhotoSuccessfullyUpdatedMessage = photoSuccessfullyUpdatedMessage;
            PhotoSuccessfullyDownloadedMessage = photoSuccessfullyDownloadedMessage;
            LinkCopiedMessage = linkCopiedMessage;
        }
    }
    public struct ReelGalleryConfigParams
    {
        public readonly int GridLayoutFixedColumnCount;
        public readonly int ThumbnailHeight;
        public readonly int ThumbnailWidth;
        public readonly bool GridShowMonth;
        public readonly bool GroupByMonth;

        public ReelGalleryConfigParams(int gridLayoutFixedColumnCount, int thumbnailHeight, int thumbnailWidth, bool gridShowMonth, bool groupByMonth)
        {
            GridLayoutFixedColumnCount = gridLayoutFixedColumnCount;
            ThumbnailHeight = thumbnailHeight;
            ThumbnailWidth = thumbnailWidth;
            GridShowMonth = gridShowMonth;
            GroupByMonth = groupByMonth;
        }
    }
}
