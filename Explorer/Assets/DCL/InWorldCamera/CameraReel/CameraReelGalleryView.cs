using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReel.Components;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.CameraReel
{
    public class CameraReelGalleryView : MonoBehaviour
    {
        private const int THUMBNAIL_POOL_DEFAULT_CAPACITY = 100;
        private const int THUMBNAIL_POOL_MAX_SIZE = 500;
        private const int GRID_POOL_DEFAULT_CAPACITY = 10;
        private const int GRID_POOL_MAX_SIZE = 500;

        [Header("References")]
        [SerializeField] internal RectTransform scrollContentRect;
        [SerializeField] internal ScrollRect scrollRect;

        [Header("Configuration")]
        public int paginationLimit = 100;
        [SerializeField] private float loadMoreScrollThreshold = 0.4f;

        [Header("Thumbnail objects")]
        [SerializeField] private ReelThumbnailView thumbnailViewPrefab;
        [SerializeField] private GameObject unusedThumbnailViewObject;

        [Header("Grid objects")]
        [SerializeField] private MonthGridView monthGridPrefab;
        [SerializeField] private GameObject unusedGridViewObject;

        private PagedCameraReelManager pagedCameraReelManager;
        private ReelGalleryPoolManager reelGalleryPoolManager;
        private ICameraReelStorageService cameraReelStorageService;
        private ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly Dictionary<DateTime, MonthGridView> monthViews = new ();
        private readonly Dictionary<MonthGridView, List<ReelThumbnailView>> reelThumbnailViews = new ();
        private readonly Dictionary<CameraReelResponse, Sprite> reelThumbnailCache = new ();
        private CancellationTokenSource loadNextPageCts = new ();
        private OptionButtonView optionsButton;

        private bool wasSetUp = false;
        private bool isLoading = false;

        public void SetUp(ICameraReelStorageService cameraReelStorageService, ICameraReelScreenshotsStorage cameraReelScreenshotsStorage)
        {
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;

            reelGalleryPoolManager ??= new ReelGalleryPoolManager(thumbnailViewPrefab, monthGridPrefab, unusedThumbnailViewObject,
                unusedGridViewObject, THUMBNAIL_POOL_DEFAULT_CAPACITY, THUMBNAIL_POOL_MAX_SIZE, GRID_POOL_DEFAULT_CAPACITY,
                GRID_POOL_MAX_SIZE);
            wasSetUp = true;
        }

        public async UniTask ShowWalletGallery(string walletAddress, OptionButtonView optionsButton, CancellationToken ct)
        {
            if (!wasSetUp)
                throw new Exception($"You must call {nameof(SetUp)} first.");

            this.optionsButton = optionsButton;
            pagedCameraReelManager = new PagedCameraReelManager(cameraReelStorageService, walletAddress, paginationLimit);
            loadNextPageCts = loadNextPageCts.SafeRestart();

            scrollRect.verticalNormalizedPosition = 1f;

            await LoadMorePage(ct);

            scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
        }

        private async UniTask LoadMorePage(CancellationToken ct)
        {
            isLoading = true;
            Dictionary<DateTime, List<CameraReelResponse>> result = await pagedCameraReelManager.FetchNextPage(ct);

            foreach (var bucket in result)
            {
                MonthGridView monthGridView;
                if (monthViews.TryGetValue(bucket.Key, out MonthGridView monthView))
                    monthGridView = monthView;
                else
                {
                     monthGridView = reelGalleryPoolManager.GetGridElement(scrollContentRect);
                     monthViews.Add(bucket.Key, monthGridView);
                }

                List<ReelThumbnailView> thumbnailViews = monthGridView.Setup(bucket.Key, bucket.Value, reelGalleryPoolManager, cameraReelScreenshotsStorage, optionsButton,
                    (cameraReelResponse, sprite) => reelThumbnailCache.Add(cameraReelResponse, sprite));

                if (reelThumbnailViews.TryGetValue(monthGridView, out List<ReelThumbnailView> thumbnails))
                    thumbnails.AddRange(thumbnailViews);
                else
                    reelThumbnailViews.Add(monthGridView, thumbnailViews);

                await UniTask.NextFrame(ct);

                isLoading = false;
            }
        }

        private void OnScrollRectValueChanged(Vector2 value)
        {
            if (value.y <= loadMoreScrollThreshold)
                OnBeforeReachScrollBottom();
        }

        private void OnBeforeReachScrollBottom()
        {
            if (pagedCameraReelManager.AllImagesLoaded || isLoading) return;

            LoadMorePage(loadNextPageCts.Token).Forget();
        }

        private void ReleaseGridViews()
        {
            foreach (MonthGridView monthGridView in monthViews.Values)
            {
                monthGridView.Release();
                reelGalleryPoolManager.ReleaseGridElement(monthGridView);
            }
            reelThumbnailViews.Clear();
            monthViews.Clear();
            reelThumbnailCache.Clear();
        }

        private void OnDisable()
        {
            ReleaseGridViews();
            scrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);
            loadNextPageCts.SafeCancelAndDispose();
        }
    }
}
