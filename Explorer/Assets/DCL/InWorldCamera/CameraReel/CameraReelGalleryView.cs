using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.InWorldCamera.CameraReel.Components;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.CameraReel
{
    public class CameraReelGalleryView : MonoBehaviour
    {
        private enum ScrollDirection
        {
            UP,
            DOWN
        }

        private const int THUMBNAIL_POOL_DEFAULT_CAPACITY = 100;
        private const int THUMBNAIL_POOL_MAX_SIZE = 10000;
        private const int GRID_POOL_DEFAULT_CAPACITY = 10;
        private const int GRID_POOL_MAX_SIZE = 500;

        [Header("References")]
        [SerializeField] private RectTransform scrollContentRect;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform elementMask;
        [SerializeField] private GameObject deleteReelModal;
        [SerializeField] private Button deleteReelButton;
        [SerializeField] private Button[] cancelDeleteIntentButtons;
        [SerializeField] private WarningNotificationView errorNotificationView;
        [SerializeField] private WarningNotificationView successNotificationView;

        [Header("Configuration")]
        public int paginationLimit = 100;
        [SerializeField] private float loadMoreScrollThreshold = 0.4f;
        [SerializeField] private float errorSuccessToastDuration = 3f;

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
        private IWebBrowser webBrowser;
        private IDecentralandUrlsSource decentralandUrlsSource;
        private readonly Dictionary<DateTime, MonthGridView> monthViews = new ();
        private readonly Dictionary<MonthGridView, List<ReelThumbnailView>> reelThumbnailViews = new ();
        private readonly Dictionary<CameraReelResponse, Sprite> reelThumbnailCache = new ();
        private ReelThumbnailView[] thumbnailImages;
        private int beginVisible;
        private int endVisible;
        private int currentSize;
        private CancellationTokenSource loadNextPageCts = new ();
        private OptionButtonView optionsButton;
        private float previousY = 1f;

        private bool wasSetUp = false;
        private bool isLoading = false;

        public event Action<CameraReelResponse> ThumbnailClicked;
        public event Action<CameraReelStorageStatus> StorageUpdated;

        public void SetUp(OptionButtonView optionsButton,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.optionsButton = optionsButton;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;

            reelGalleryPoolManager ??= new ReelGalleryPoolManager(thumbnailViewPrefab, monthGridPrefab, unusedThumbnailViewObject,
                unusedGridViewObject, THUMBNAIL_POOL_DEFAULT_CAPACITY, THUMBNAIL_POOL_MAX_SIZE, GRID_POOL_DEFAULT_CAPACITY,
                GRID_POOL_MAX_SIZE);

            for(int i = 0; i < cancelDeleteIntentButtons.Length; i++)
                cancelDeleteIntentButtons[i].onClick.AddListener(OnDeletionModalCancelClick);
            deleteReelButton?.onClick.AddListener(DeleteScreenshot);

            this.optionsButton.SetPublicRequested += (cameraReelRes, publicFlag) =>
            {

            };

            this.optionsButton.ShareToXRequested += cameraReelResponse =>
            {
                string description = "Check out what I'm doing in Decentraland right now and join me!".Replace(" ", "%20");
                string url = $"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink)}/{cameraReelResponse.id}";
                string xUrl = $"https://x.com/intent/post?text={description}&hashtags=DCLCamera&url={url}";

                EditorGUIUtility.systemCopyBuffer = xUrl;
                webBrowser.OpenUrl(xUrl);
            };

            this.optionsButton.CopyPictureLinkRequested += cameraReelResponse =>
            {
                EditorGUIUtility.systemCopyBuffer = $"{decentralandUrlsSource.Url(DecentralandUrl.CameraReelLink)}/{cameraReelResponse.id}";
                ShowSuccessNotification("Link copied!").Forget();
            };

            this.optionsButton.DownloadRequested += cameraReelResponse =>
            {
                webBrowser.OpenUrl(cameraReelResponse.url);
            };

            wasSetUp = true;
        }

        private void OnDeletionModalCancelClick() =>
            deleteReelModal.SetActive(false);

        private void DeletionOptionClicked(CameraReelResponse response) =>
            deleteReelModal.SetActive(true);

        private void DeleteScreenshot()
        {
            if (optionsButton?.ImageData is null) return;
            DeleteScreenshotsAsync(optionsButton.ImageData).Forget();
            OnDeletionModalCancelClick();
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

                if (successNotificationView is null) return;

                await ShowSuccessNotification("Photo successfully deleted", ct);
            }
            catch (Exception)
            {
                if (errorNotificationView is null) return;

                errorNotificationView.Show();
                await UniTask.Delay((int) errorSuccessToastDuration * 1000, cancellationToken: ct);
                errorNotificationView.Hide();
            }
        }

        public async UniTask ShowWalletGallery(string walletAddress, CancellationToken ct)
        {
            if (!wasSetUp)
                throw new Exception($"You must call {nameof(SetUp)} first.");

            pagedCameraReelManager = new PagedCameraReelManager(cameraReelStorageService, walletAddress, paginationLimit);
            loadNextPageCts = loadNextPageCts.SafeRestart();

            scrollRect.verticalNormalizedPosition = 1f;
            previousY = 1f;

            CameraReelStorageStatus storageStatus = await cameraReelStorageService.GetUserGalleryStorageInfoAsync(walletAddress, ct);

            thumbnailImages = new ReelThumbnailView[storageStatus.MaxScreenshots];

            await LoadMorePage(true, ct);

            scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);

            if (this.optionsButton is not null)
            {
                this.optionsButton.DeletePictureRequested += DeletionOptionClicked;
            }
        }

        private async UniTask ShowSuccessNotification(string message, CancellationToken ct = default)
        {
            successNotificationView.SetText(message);
            successNotificationView.Show();
            await UniTask.Delay((int) errorSuccessToastDuration * 1000, cancellationToken: ct);
            successNotificationView.Hide();
        }

        private MonthGridView GetMonthGridView(DateTime dateTime)
        {
            MonthGridView monthGridView;
            if (monthViews.TryGetValue(dateTime, out MonthGridView monthView))
                monthGridView = monthView;
            else
            {
                monthGridView = reelGalleryPoolManager.GetGridElement(scrollContentRect);
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

                List<ReelThumbnailView> thumbnailViews = monthGridView.Setup(bucket.Key, bucket.Value, reelGalleryPoolManager, cameraReelScreenshotsStorage, optionsButton,
                    (cameraReelResponse, sprite) => reelThumbnailCache.Add(cameraReelResponse, sprite),
                    cameraReelResponse => ThumbnailClicked?.Invoke(cameraReelResponse));

                if (reelThumbnailViews.TryGetValue(monthGridView, out List<ReelThumbnailView> thumbnails))
                    thumbnails.AddRange(thumbnailViews);
                else
                    reelThumbnailViews.Add(monthGridView, thumbnailViews);

                for (int i = 0; i < thumbnailViews.Count; i++)
                    thumbnailImages[currentSize + i] = thumbnailViews[i];

                currentSize += thumbnailViews.Count;
                endVisible = currentSize - 1;
            }

            await UniTask.NextFrame(ct);

            if (firstLoading)
                for (int i = beginVisible; i < currentSize && ViewIntersectsImage(thumbnailImages[i].thumbnailImage); i++)
                    EnableThumbnailImage(thumbnailImages[i]);
            else
                CheckElementsVisibility(ScrollDirection.UP);

            previousY = scrollRect.verticalNormalizedPosition;
            isLoading = false;
        }

        private void OnScrollRectValueChanged(Vector2 value)
        {
            if (value.y <= loadMoreScrollThreshold)
                OnBeforeReachScrollBottom();

            CheckElementsVisibility(value.y > previousY ? ScrollDirection.UP : ScrollDirection.DOWN);

            previousY = value.y;
        }

        private void OnBeforeReachScrollBottom()
        {
            if (pagedCameraReelManager.AllImagesLoaded || isLoading) return;

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
            var view = elementMask.GetWorldRect();
            var img = image.rectTransform.GetWorldRect();

            return img.yMax >= view.yMin && img.yMin < view.yMax;
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
            scrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);

            if (optionsButton is not null)
            {
                optionsButton.DeletePictureRequested -= DeletionOptionClicked;
            }
            loadNextPageCts.SafeCancelAndDispose();
        }
    }

}
