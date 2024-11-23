using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.ReelActions;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailController : ControllerBase<PhotoDetailView, PhotoDetailParameter>
    {
        private readonly PhotoDetailInfoController photoDetailInfoController;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ISystemClipboard systemClipboard;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWebBrowser webBrowser;
        private readonly string shareToXMessage;

        private MetadataSidePanelAnimator metadataSidePanelAnimator;
        private CancellationTokenSource showReelCts = new ();

        private bool isClosing;
        private bool metadataPanelIsOpen = true;
        private int currentReelIndex;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public PhotoDetailController(ViewFactoryMethod viewFactory,
            PhotoDetailInfoController photoDetailInfoController,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ISystemClipboard systemClipboard,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWebBrowser webBrowser,
            string shareToXMessage)
            : base(viewFactory)
        {
            this.photoDetailInfoController = photoDetailInfoController;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.systemClipboard = systemClipboard;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.webBrowser = webBrowser;
            this.shareToXMessage = shareToXMessage;
        }

        private void ToggleInfoSidePanel()
        {
            metadataSidePanelAnimator.ToggleSizeMode(toFullScreen: metadataPanelIsOpen, viewInstance.sidePanelAnimationDuration);
            metadataPanelIsOpen = !metadataPanelIsOpen;
        }

        protected override void OnViewShow()
        {
            viewInstance!.closeButton.onClick.AddListener(CloseButtonClicked);
            viewInstance.infoButton.onClick.AddListener(ToggleInfoSidePanel);
            viewInstance!.previousScreenshotButton.onClick.AddListener(ShowPreviousReel);
            viewInstance!.nextScreenshotButton.onClick.AddListener(ShowNextReel);
            viewInstance!.downloadButton.onClick.AddListener(DownloadReelClicked);
            viewInstance!.linkButton.onClick.AddListener(CopyReelLinkClicked);
            viewInstance!.twitterButton.onClick.AddListener(ShareReelClicked);

            ShowReel(inputData.CurrentReelIndex);
        }

        protected override void OnViewInstantiated()
        {
            metadataSidePanelAnimator = new MetadataSidePanelAnimator(viewInstance!.rootContainer, viewInstance.infoButtonImageRectTransform);
        }

        protected override void OnViewClose()
        {
            viewInstance!.closeButton.onClick.RemoveListener(CloseButtonClicked);
            viewInstance.infoButton.onClick.RemoveListener(ToggleInfoSidePanel);
            viewInstance!.previousScreenshotButton.onClick.RemoveListener(ShowPreviousReel);
            viewInstance!.nextScreenshotButton.onClick.RemoveListener(ShowNextReel);
            viewInstance!.downloadButton.onClick.RemoveListener(DownloadReelClicked);
            viewInstance!.linkButton.onClick.RemoveListener(CopyReelLinkClicked);
            viewInstance!.twitterButton.onClick.RemoveListener(ShareReelClicked);

            viewInstance.mainImageCanvasGroup.alpha = 0;
            photoDetailInfoController.Release();
        }

        protected override void OnBeforeViewShow()
        {
            isClosing = false;
            currentReelIndex = inputData.CurrentReelIndex;
            viewInstance!.deleteButton.gameObject.SetActive(inputData.UserOwnedReels);
            viewInstance!.previousScreenshotButton.gameObject.SetActive(false);
            viewInstance!.nextScreenshotButton.gameObject.SetActive(false);
        }

        private void DownloadReelClicked() =>
            ReelCommonActions.DownloadReel(inputData.AllReels[currentReelIndex].url, webBrowser);

        private void CopyReelLinkClicked() =>
            ReelCommonActions.CopyReelLink(inputData.AllReels[currentReelIndex].id, decentralandUrlsSource, systemClipboard);

        private void ShareReelClicked() =>
            ReelCommonActions.ShareReelToX(shareToXMessage, inputData.AllReels[currentReelIndex].id, decentralandUrlsSource, systemClipboard, webBrowser);

        private void ShowPreviousReel()
        {
            currentReelIndex = Math.Clamp(currentReelIndex - 1, 0, inputData.AllReels.Count - 1);
            ShowReel(currentReelIndex);
        }

        private void ShowNextReel()
        {
            currentReelIndex = Math.Clamp(currentReelIndex + 1, 0, inputData.AllReels.Count - 1);
            ShowReel(currentReelIndex);
        }

        private void ShowReel(int reelIndex)
        {
            showReelCts = showReelCts.SafeRestart();
            ShowReelAsync(reelIndex, showReelCts.Token).Forget();
        }

        private async UniTaskVoid ShowReelAsync(int reelIndex, CancellationToken ct)
        {
            viewInstance!.mainImageLoadingSpinner.gameObject.SetActive(true);
            CameraReelResponseCompact reel = inputData.AllReels[reelIndex];

            UniTask detailInfoTask = photoDetailInfoController.ShowPhotoDetailInfoAsync(reel.id, ct);
            Texture2D reelTexture = await cameraReelScreenshotsStorage.GetScreenshotImageAsync(reel.url, ct);
            viewInstance!.mainImage.sprite = Sprite.Create(reelTexture, new Rect(0, 0, reelTexture.width, reelTexture.height), Vector2.zero);

            viewInstance!.mainImageLoadingSpinner.gameObject.SetActive(false);
            viewInstance.mainImageCanvasGroup.DOFade(1, viewInstance.imageFadeInDuration);

            await detailInfoTask;

            CheckNavigationButtonVisibility(inputData.AllReels, reelIndex);
        }

        private void CheckNavigationButtonVisibility(List<CameraReelResponseCompact> allReels, int index)
        {
            viewInstance!.previousScreenshotButton.gameObject.SetActive(index != 0);
            viewInstance!.nextScreenshotButton.gameObject.SetActive(index != allReels.Count - 1);
        }

        private void CloseButtonClicked() =>
            isClosing = true;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WaitWhile(() => !isClosing, cancellationToken: ct);

    }

    public readonly struct PhotoDetailParameter
    {
        public readonly List<CameraReelResponseCompact> AllReels;
        public readonly int CurrentReelIndex;
        public readonly bool UserOwnedReels;

        public PhotoDetailParameter(List<CameraReelResponseCompact> allReels, int currentReelIndex, bool userOwnedReels)
        {
            this.AllReels = allReels;
            this.CurrentReelIndex = currentReelIndex;
            this.UserOwnedReels = userOwnedReels;
        }
    }
}
