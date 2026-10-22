using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.InWorldCamera.CameraReelGallery.Components;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.CameraReelToast;
using DCL.InWorldCamera.ReelActions;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using DCL.UI.Utilities;
using DG.Tweening;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.CameraReelGallery
{
    public class CameraReelGalleryController : IDisposable
    {
        public delegate void ThumbnailClick(
            List<CameraReelResponseCompact> reels,
            int index,
            Action<CameraReelResponseCompact> reelDeleteIntention,
            Action<CameraReelResponseCompact> reelListRefreshIntention);

        // Order of the item prefabs in the LoopListView2 of CameraReelGallery.prefab.
        private const int PADDING_PREFAB_INDEX = 0;
        private const int MONTH_HEADER_PREFAB_INDEX = 1;
        private const int THUMBNAIL_ROW_PREFAB_INDEX = 2;
        private const int ANIMATION_DELAY = 300;

        private readonly CameraReelGalleryView view;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ISpriteCache reelThumbnailCache;
        private readonly IDecentralandUrlsSource? decentralandUrlsSource;
        private readonly ISystemClipboard? systemClipboard;
        private readonly UnityAppWebBrowser? webBrowser;
        private readonly GalleryEventBus galleryEventBus;
        private readonly CameraReelOptionButtonController? optionButtonController;
        private readonly CameraReelGalleryMessagesConfiguration? reelGalleryStringMessages;
        private readonly ReelGalleryConfigParams reelGalleryConfigParams;
        private readonly bool useSignedRequest;
        private readonly List<ReelGalleryRow> rows = new ();
        private readonly Action<int> onThumbnailClicked;
        private readonly Action<CameraReelResponseCompact> deleteReelIntention;
        private readonly Action<CameraReelResponseCompact> hideReelIntention;

        private bool isLoading;
        private bool isBindingRows;
        private int highestBoundIndex = -1;
        private CancellationTokenSource loadNextPageCts = new ();
        private CancellationTokenSource setPublicCts = new ();
        private CancellationTokenSource deleteScreenshotCts = new ();
        private CancellationTokenSource downloadScreenshotCts = new ();
        private CameraReelResponseCompact? reelToDelete;
        private PagedCameraReelManager? pagedCameraReelManager;

        public event ThumbnailClick? ThumbnailClicked;
        public event Action<CameraReelStorageStatus>? StorageUpdated;
        public event Action? ScreenshotDeleted;
        public event Action? ScreenshotShared;
        public event Action? ScreenshotDownloaded;
        public event Action<int>? MaxThumbnailsUpdated;

        public CameraReelGalleryController(CameraReelGalleryView view,
            ICameraReelStorageService cameraReelStorageService,
            ISpriteCache reelThumbnailCache,
            ReelGalleryConfigParams reelGalleryConfigParams,
            bool useSignedRequest,
            GalleryEventBus galleryEventBus,
            CameraReelOptionButtonView? optionButtonView = null,
            UnityAppWebBrowser? webBrowser = null,
            IDecentralandUrlsSource? decentralandUrlsSource = null,
            ISystemClipboard? systemClipboard = null,
            CameraReelGalleryMessagesConfiguration? reelGalleryStringMessages = null,
            IMVCManager? mvcManager = null)
        {
            this.view = view;
            this.cameraReelStorageService = cameraReelStorageService;
            this.reelThumbnailCache = reelThumbnailCache;
            this.reelGalleryConfigParams = reelGalleryConfigParams;
            this.reelGalleryStringMessages = reelGalleryStringMessages;
            this.view.Disable += OnDisable;
            this.useSignedRequest = useSignedRequest;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.systemClipboard = systemClipboard;
            this.webBrowser = webBrowser;
            this.galleryEventBus = galleryEventBus;

            onThumbnailClicked = OnThumbnailClicked;
            deleteReelIntention = DeleteReelFromDetail;
            hideReelIntention = HideReelFromList;

            view.loopList.InitListView(0, OnGetItemByIndex, new LoopListViewInitParam
                { mItemDefaultWithPaddingSize = reelGalleryConfigParams.ThumbnailHeight + ReelGalleryRow.ROW_SPACING });

            this.view.scrollRect.SetScrollSensitivityBasedOnPlatform();

            if (optionButtonView is not null)
                this.optionButtonController = new CameraReelOptionButtonController(optionButtonView, mvcManager!, reelGalleryConfigParams.EnableDeleteContextOption);

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

                if (reelGalleryConfigParams.HideReelOnPrivateSet)
                    this.optionButtonController.SetPublicRequested += HideReelIfSetPrivate;
            }
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
            downloadScreenshotCts.SafeCancelAndDispose();

            optionButtonController?.Dispose();

            if (this.optionButtonController != null)
            {
                this.optionButtonController.SetPublicRequested -= SetReelPublic;
                this.optionButtonController.ShareToXRequested -= ShareToX;
                this.optionButtonController.CopyPictureLinkRequested -= CopyPictureLink;
                this.optionButtonController.DownloadRequested -= DownloadReelLocally;
                this.optionButtonController.DeletePictureRequested -= DeleteReel;

                this.optionButtonController.SetPublicRequested -= HideReelIfSetPrivate;
            }
        }

        public void TryEnableContextMenuButton(bool enable)
        {
            if (optionButtonController != null)
                optionButtonController.enableContextMenuButton = enable;
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

            await ShowGalleryAsync(new PagedCameraReelManagerParameters(walletAddress, useSignedRequest), storageStatus.Value.ScreenshotsAmount, ct);
        }

        public async UniTask ShowPlaceGalleryAsync(string placeId, CancellationToken ct)
        {
            PrepareShowGallery(ct);

            CameraReelStorageStatus storageStatus = await cameraReelStorageService.GetPlaceGalleryStorageInfoAsync(placeId, ct);

            await ShowGalleryAsync(new PagedCameraReelManagerParameters(placeId), storageStatus.ScreenshotsAmount, ct);
        }

        public async UniTask ShowPlacesGalleryAsync(string[] placeIds, CancellationToken ct)
        {
            PrepareShowGallery(ct);

            CameraReelStorageStatus storageStatus = await cameraReelStorageService.GetPlacesGalleryStorageInfoAsync(placeIds, ct);

            if (storageStatus.MaxScreenshots == 0)
            {
                view.emptyState.SetActive(true);
                FinishShowGallery();
                return;
            }

            await ShowGalleryAsync(new PagedCameraReelManagerParameters(placeIds), storageStatus.MaxScreenshots, ct);
        }

        private void PrepareShowGallery(CancellationToken ct)
        {
            view.loadingSpinner.SetActive(true);
            view.emptyState.SetActive(false);
            loadNextPageCts = loadNextPageCts.SafeRestartLinked(ct);
            setPublicCts = setPublicCts.SafeRestart();
            deleteScreenshotCts = deleteScreenshotCts.SafeRestart();

            pagedCameraReelManager = null;
            ClearRows();
        }

        private async UniTask ShowGalleryAsync(PagedCameraReelManagerParameters parameters, int totalImages, CancellationToken ct)
        {
            var manager = new PagedCameraReelManager(cameraReelStorageService, parameters, totalImages, view.PaginationLimit);
            pagedCameraReelManager = manager;

            await LoadMorePageAsync(manager, ct);

            FinishShowGallery();
        }

        private void FinishShowGallery()
        {
            view.loadingSpinner.SetActive(false);
            galleryEventBus.ReelPublicStateChangeEvent += OnReelPublicStateChange;
            galleryEventBus.ReelDeletedEvent += OnReelDeletionSignal;
        }

        private async UniTask LoadMorePageAsync(PagedCameraReelManager manager, CancellationToken ct)
        {
            isLoading = true;

            try
            {
                await manager.FetchNextPageAsync(ct);

                if (ct.IsCancellationRequested)
                    return;

                RebuildRows(manager, resetPosition: false);
                MaxThumbnailsUpdated?.Invoke(manager.AllOrderedResponses.Count);
            }
            finally { isLoading = false; }

            // The rows bound above were gated by isLoading; a short page may leave the list within the threshold.
            TryLoadMore(manager, highestBoundIndex);
        }

        private async UniTaskVoid LoadMorePageInBackgroundAsync(PagedCameraReelManager manager, CancellationToken ct)
        {
            try { await LoadMorePageAsync(manager, ct); }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL)); }
        }

        private void TryLoadMore(PagedCameraReelManager manager, int lastBoundIndex)
        {
            if (isLoading || isBindingRows || manager.AllImagesLoaded)
                return;

            if (manager.AllOrderedResponses.Count - lastBoundIndex >= view.loadMoreCounterThreshold)
                return;

            LoadMorePageInBackgroundAsync(manager, loadNextPageCts.Token).Forget();
        }

        private void RebuildRows(PagedCameraReelManager manager, bool resetPosition)
        {
            ReelGalleryRow.Build(rows, manager.AllOrderedResponses, in reelGalleryConfigParams, view.paddingTop, view.paddingBottom, view.monthSpacing);

            // Binding runs synchronously inside these calls; a page request started from a bind would re-enter the list.
            isBindingRows = true;
            view.loopList.SetListItemCount(rows.Count, resetPosition);
            view.loopList.RefreshAllShownItem();
            isBindingRows = false;
        }

        private void ClearRows()
        {
            rows.Clear();
            highestBoundIndex = -1;
            view.loopList.SetListItemCount(0);
        }

        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 list, int index)
        {
            PagedCameraReelManager? manager = pagedCameraReelManager;

            if (manager == null || index < 0 || index >= rows.Count)
                return null;

            ReelGalleryRow row = rows[index];
            LoopListViewItem2 item;

            switch (row.Kind)
            {
                case ReelGalleryRowKind.Padding:
                    item = list.NewListViewItem(PrefabName(list, PADDING_PREFAB_INDEX));
                    item.CachedRectTransform.sizeDelta = new Vector2(item.CachedRectTransform.sizeDelta.x, row.Size);
                    break;
                case ReelGalleryRowKind.MonthHeader:
                    item = list.NewListViewItem(PrefabName(list, MONTH_HEADER_PREFAB_INDEX));
                    item.GetComponent<ReelGalleryMonthHeaderView>().SetMonth(row.Month);
                    break;
                default:
                    item = list.NewListViewItem(PrefabName(list, THUMBNAIL_ROW_PREFAB_INDEX));
                    ReelGalleryRowView rowView = item.GetComponent<ReelGalleryRowView>();

                    if (!rowView.IsInitialized)
                        rowView.Initialize(view.thumbnailViewPrefab, in reelGalleryConfigParams, optionButtonController, onThumbnailClicked);

                    rowView.Bind(manager.AllOrderedResponses, row.StartIndex, row.Count, reelThumbnailCache);

                    int lastIndex = row.StartIndex + row.Count - 1;
                    highestBoundIndex = Math.Max(highestBoundIndex, lastIndex);
                    TryLoadMore(manager, lastIndex);
                    break;
            }

            item.Padding = row.SpacingAfter;
            item.StartPosOffset = view.paddingLeft;
            return item;
        }

        private static string PrefabName(LoopListView2 list, int prefabIndex) =>
            list.ItemPrefabDataList[prefabIndex].mItemPrefab.name;

        private void OnThumbnailClicked(int index)
        {
            if (pagedCameraReelManager == null) return;

            ThumbnailClicked?.Invoke(pagedCameraReelManager.AllOrderedResponses, index, deleteReelIntention, hideReelIntention);
        }

        private void DeleteReelFromDetail(CameraReelResponseCompact reel)
        {
            reelToDelete = reel;
            DeleteScreenshot();
        }

        private void HideReelIfSetPrivate(CameraReelResponseCompact response, bool isPublic)
        {
            if (isPublic) return;

            HideReelFromList(response);
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
                    await ReelCommonActions.DownloadReelToFileAsync(response.url, ct);
                    ScreenshotDownloaded?.Invoke();

                    view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.Download,
                        reelGalleryStringMessages?.PhotoSuccessfullyDownloadedMessage);
                }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL));
                    view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.Failure);
                }
            }

            downloadScreenshotCts = downloadScreenshotCts.SafeRestart();
            DownloadAndOpenAsync(downloadScreenshotCts.Token).Forget();
        }

        private void CopyPictureLink(CameraReelResponseCompact response)
        {
            ReelCommonActions.CopyReelLink(response.id, decentralandUrlsSource!, systemClipboard!);
            view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.Success, reelGalleryStringMessages?.LinkCopiedMessage);
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
                    await cameraReelStorageService.UpdateScreenshotVisibilityAsync(response.id, isPublic, ct);
                    response.isPublic = isPublic;
                    view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.Success, reelGalleryStringMessages?.PhotoSuccessfullyUpdatedMessage);
                }
                catch (UnityWebRequestException e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL));
                    view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.Failure);
                }
            }

            SetPublicFlagAsync(setPublicCts.Token).Forget();
        }

        private void ShowDeleteModal()
        {
            if (view.deleteReelModal == null) return;

            view.deleteReelModal.gameObject.SetActive(true);
            view.deleteReelModal.DOFade(1f, view.deleteModalAnimationDuration);
        }

        private void HideDeleteModal()
        {
            CanvasGroup? deleteReelModal = view.deleteReelModal;

            if (deleteReelModal == null) return;

            deleteReelModal.DOFade(0f, view.deleteModalAnimationDuration).OnComplete(() => deleteReelModal.gameObject.SetActive(false));
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
                    DeleteScreenshotsAsync(reelToDelete.id, deleteScreenshotCts.Token).Forget();

                reelToDelete = null;
                HideDeleteModal();
            }

            AnimateAndAwaitAsync().Forget();
        }

        private async UniTask DeleteScreenshotsAsync(string reelId, CancellationToken ct = default)
        {
            try
            {
                CameraReelStorageStatus response = await cameraReelStorageService.DeleteScreenshotAsync(reelId, ct);
                galleryEventBus.ReelDeleted(reelId);
                RemoveThumbnailFromList(reelId);

                ScreenshotDeleted?.Invoke();
                StorageUpdated?.Invoke(response);

                view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.Success, reelGalleryStringMessages?.PhotoSuccessfullyDeletedMessage);
            }
            catch (UnityWebRequestException e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL));

                view.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.Failure);
            }
        }

        private void HideReelFromList(CameraReelResponseCompact reelToHide)
        {
            RemoveThumbnailFromList(reelToHide.id);
        }

        private void RemoveThumbnailFromList(string reelId)
        {
            PagedCameraReelManager? manager = pagedCameraReelManager;

            // Idempotent: the deletion signal reaches every open gallery, loaded or not.
            if (manager == null || !manager.RemoveReelId(reelId))
                return;

            RebuildRows(manager, resetPosition: false);
            TryLoadMore(manager, highestBoundIndex);

            if (manager.AllOrderedResponses.Count == 0)
                view.emptyState.SetActive(true);
        }

        private void OnReelPublicStateChange(string reelId, bool isPublic)
        {
            if (pagedCameraReelManager == null) return;

            foreach (CameraReelResponseCompact thumbnail in pagedCameraReelManager.AllOrderedResponses)
            {
                if (thumbnail.id != reelId) continue;

                thumbnail.isPublic = isPublic;
                return;
            }
        }

        private void OnReelDeletionSignal(string reelId)
        {
            RemoveThumbnailFromList(reelId);
        }

        private void OnDisable()
        {
            ClearRows();
            pagedCameraReelManager = null;

            // Thumbnails must not outlive the gallery.
            reelThumbnailCache.Clear();

            loadNextPageCts.SafeCancelAndDispose();
            setPublicCts.SafeCancelAndDispose();
            deleteScreenshotCts.SafeCancelAndDispose();

            HideDeleteModal();
            optionButtonController?.HideControl();

            galleryEventBus.ReelPublicStateChangeEvent -= OnReelPublicStateChange;
            galleryEventBus.ReelDeletedEvent -= OnReelDeletionSignal;
        }
    }

    public readonly struct ReelGalleryConfigParams
    {
        public readonly int GridLayoutFixedColumnCount;
        public readonly int ThumbnailHeight;
        public readonly int ThumbnailWidth;
        public readonly bool GridShowMonth;
        public readonly bool GroupByMonth;
        public readonly bool EnableDeleteContextOption;
        public readonly bool HideReelOnPrivateSet;

        public ReelGalleryConfigParams(int gridLayoutFixedColumnCount, int thumbnailHeight, int thumbnailWidth, bool gridShowMonth, bool groupByMonth, bool enableDeleteContextOption = true, bool hideReelOnPrivateSet = false)
        {
            GridLayoutFixedColumnCount = gridLayoutFixedColumnCount;
            ThumbnailHeight = thumbnailHeight;
            ThumbnailWidth = thumbnailWidth;
            GridShowMonth = gridShowMonth;
            GroupByMonth = groupByMonth;
            EnableDeleteContextOption = enableDeleteContextOption;
            HideReelOnPrivateSet = hideReelOnPrivateSet;
        }
    }
}
