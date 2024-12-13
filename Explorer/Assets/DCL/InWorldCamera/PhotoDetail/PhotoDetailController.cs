using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Diagnostics;
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
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.PhotoDetail
{
    /// <summary>
    ///     Handles the logic for the photo detail view and his macro-actions.
    /// </summary>
    public class PhotoDetailController : ControllerBase<PhotoDetailView, PhotoDetailParameter>
    {
        private const int ANIMATION_DELAY = 300;

        public event Action Activated;
        public event Action JumpToPhotoPlace;

        public readonly PhotoDetailInfoController PhotoDetailInfoController;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ISystemClipboard systemClipboard;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWebBrowser webBrowser;
        private readonly string shareToXMessage;

        private AspectRatioFitter aspectRatioFitter;
        private MetadataSidePanelAnimator metadataSidePanelAnimator;
        private CancellationTokenSource showReelCts = new ();
        private CancellationTokenSource downloadScreenshotCts = new ();

        private bool metadataPanelIsOpen = true;
        private bool isClosing;
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
            this.PhotoDetailInfoController = photoDetailInfoController;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.systemClipboard = systemClipboard;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.webBrowser = webBrowser;
            this.shareToXMessage = shareToXMessage;

            this.PhotoDetailInfoController.JumpIn += JumpInClicked;
        }

        private void ShowDeleteModal()
        {
            viewInstance!.deleteReelModal.gameObject.SetActive(true);
            viewInstance.deleteReelModal.DOFade(1f, viewInstance.deleteModalAnimationDuration);
        }

        private void HideDeleteModal(InputAction.CallbackContext callbackContext = default) =>
            viewInstance!.deleteReelModal.DOFade(0f, viewInstance.deleteModalAnimationDuration).OnComplete(() => viewInstance.deleteReelModal.gameObject.SetActive(false));

        private void DeletionModalCancelClick(bool waitForAnimation = true)
        {
            async UniTaskVoid DelayedHideDeleteModalAsync()
            {
                await UniTask.Delay(ANIMATION_DELAY);
                HideDeleteModal();
            }

            if (waitForAnimation)
                DelayedHideDeleteModalAsync().Forget();
            else
                HideDeleteModal();
        }

        private void JumpInClicked()
        {
            isClosing = true;
            JumpToPhotoPlace?.Invoke();
        }

        private void ToggleInfoSidePanel()
        {
            metadataSidePanelAnimator.ToggleSizeMode(toFullScreen: metadataPanelIsOpen, viewInstance.sidePanelAnimationDuration);
            metadataPanelIsOpen = !metadataPanelIsOpen;
        }

        protected override void OnViewShow()
        {
            viewInstance!.infoButton.onClick.AddListener(ToggleInfoSidePanel);
            viewInstance!.previousScreenshotButton.onClick.AddListener(ShowPreviousReel);
            viewInstance!.nextScreenshotButton.onClick.AddListener(ShowNextReel);
            viewInstance!.downloadButton.onClick.AddListener(DownloadReelClicked);
            viewInstance!.linkButton.onClick.AddListener(CopyReelLinkClicked);
            viewInstance!.twitterButton.onClick.AddListener(ShareReelClicked);
            viewInstance!.deleteButton.onClick.AddListener(ShowDeleteModal);
            viewInstance!.cancelDeleteIntentButton?.onClick.AddListener(() => DeletionModalCancelClick());
            viewInstance!.cancelDeleteIntentBackgroundButton?.onClick.AddListener(() => DeletionModalCancelClick(false));
            viewInstance!.deleteReelButton?.onClick.AddListener(DeleteScreenshot);

            Activated?.Invoke();

            ShowReel(inputData.CurrentReelIndex);
        }

        private void DeleteScreenshot()
        {
            if (!inputData.UserOwnedReels) return;

            inputData.ExecuteDeleteAction(currentReelIndex);
            isClosing = true;
        }

        protected override void OnViewInstantiated()
        {
            metadataSidePanelAnimator = new MetadataSidePanelAnimator(viewInstance!.rootContainer, viewInstance.infoButtonImageRectTransform);
            aspectRatioFitter = viewInstance.mainImage.GetComponent<AspectRatioFitter>();
        }

        protected override void OnViewClose()
        {
            viewInstance!.infoButton.onClick.RemoveListener(ToggleInfoSidePanel);
            viewInstance!.previousScreenshotButton.onClick.RemoveListener(ShowPreviousReel);
            viewInstance!.nextScreenshotButton.onClick.RemoveListener(ShowNextReel);
            viewInstance!.downloadButton.onClick.RemoveListener(DownloadReelClicked);
            viewInstance!.linkButton.onClick.RemoveListener(CopyReelLinkClicked);
            viewInstance!.twitterButton.onClick.RemoveListener(ShareReelClicked);
            viewInstance!.deleteButton.onClick.RemoveListener(ShowDeleteModal);
            HideDeleteModal();

            viewInstance.mainImageCanvasGroup.alpha = 0;
            PhotoDetailInfoController.Release();
        }

        protected override void OnBeforeViewShow()
        {
            currentReelIndex = inputData.CurrentReelIndex;
            viewInstance!.deleteButton.gameObject.SetActive(inputData.UserOwnedReels);
            viewInstance!.previousScreenshotButton.gameObject.SetActive(false);
            viewInstance!.nextScreenshotButton.gameObject.SetActive(false);
            isClosing = false;
        }

        private void DownloadReelClicked()
        {
            async UniTaskVoid DownloadAndOpenAsync(CancellationToken ct)
            {
                try
                {
                    await ReelCommonActions.DownloadReelToFileAsync(inputData.AllReels[currentReelIndex].url, ct);
                }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL));
                }
            }

            DownloadAndOpenAsync(downloadScreenshotCts.Token).Forget();
        }

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
            CheckNavigationButtonVisibility(inputData.AllReels, reelIndex);
            showReelCts = showReelCts.SafeRestart();
            ShowReelAsync(reelIndex, showReelCts.Token).Forget();
        }

        private async UniTaskVoid ShowReelAsync(int reelIndex, CancellationToken ct)
        {
            viewInstance!.mainImageCanvasGroup.alpha = 0;
            viewInstance!.mainImageLoadingSpinner.gameObject.SetActive(true);
            CameraReelResponseCompact reel = inputData.AllReels[reelIndex];

            UniTask detailInfoTask = PhotoDetailInfoController.ShowPhotoDetailInfoAsync(reel.id, ct);
            Texture2D reelTexture = await cameraReelScreenshotsStorage.GetScreenshotImageAsync(reel.url, ct);
            viewInstance!.mainImage.texture = reelTexture;
            aspectRatioFitter.aspectRatio = reelTexture.width * 1f / reelTexture.height;

            viewInstance!.mainImageLoadingSpinner.gameObject.SetActive(false);
            viewInstance.mainImageCanvasGroup.DOFade(1, viewInstance.imageFadeInDuration);

            await detailInfoTask;
        }

        private void CheckNavigationButtonVisibility(List<CameraReelResponseCompact> allReels, int index)
        {
            viewInstance!.previousScreenshotButton.gameObject.SetActive(index != 0);
            viewInstance!.nextScreenshotButton.gameObject.SetActive(index != allReels.Count - 1);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance.closeButton.OnClickAsync(ct),UniTask.WaitUntil(() => isClosing, cancellationToken: ct));
    }
}
