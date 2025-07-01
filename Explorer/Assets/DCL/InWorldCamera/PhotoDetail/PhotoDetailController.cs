using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.CameraReelToast;
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
        public event Action ScreenshotShared;
        public event Action ScreenshotDownloaded;

        public readonly PhotoDetailInfoController PhotoDetailInfoController;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ISystemClipboard systemClipboard;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWebBrowser webBrowser;
        private readonly PhotoDetailStringMessages photoDetailStringMessages;

        private AspectRatioFitter aspectRatioFitter;
        private MetadataSidePanelAnimator metadataSidePanelAnimator;
        private CancellationTokenSource showReelCts = new ();
        private CancellationTokenSource downloadScreenshotCts = new ();
        private CancellationTokenSource setPublicCts = new ();
        private CancellationTokenSource closePanelCts = new ();

        private bool metadataPanelIsOpen = true;
        private bool isClosing;
        private int currentReelIndex;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public PhotoDetailController(ViewFactoryMethod viewFactory,
            PhotoDetailInfoController photoDetailInfoController,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ICameraReelStorageService cameraReelStorageService,
            ISystemClipboard systemClipboard,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWebBrowser webBrowser,
            PhotoDetailStringMessages photoDetailStringMessages)
            : base(viewFactory)
        {
            this.PhotoDetailInfoController = photoDetailInfoController;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.cameraReelStorageService = cameraReelStorageService;
            this.systemClipboard = systemClipboard;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.webBrowser = webBrowser;
            this.photoDetailStringMessages = photoDetailStringMessages;

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
            viewInstance.setAsPublicToggle.gameObject.SetActive(!inputData.OpenedFromPublicBoard);
            viewInstance.previousScreenshotButton.onClick.AddListener(ShowPreviousReel);
            viewInstance.nextScreenshotButton.onClick.AddListener(ShowNextReel);
            viewInstance.downloadButton.onClick.AddListener(DownloadReelClicked);
            viewInstance.linkButton.onClick.AddListener(CopyReelLinkClicked);
            viewInstance.twitterButton.onClick.AddListener(ShareReelClicked);
            viewInstance.deleteButton.gameObject.SetActive(!inputData.OpenedFromPublicBoard);
            viewInstance.deleteButton.Button.onClick.AddListener(ShowDeleteModal);
            viewInstance.cancelDeleteIntentButton?.onClick.AddListener(() => DeletionModalCancelClick());
            viewInstance.cancelDeleteIntentBackgroundButton?.onClick.AddListener(() => DeletionModalCancelClick(false));
            viewInstance.deleteReelButton?.onClick.AddListener(DeleteScreenshot);


            Activated?.Invoke();

            ShowReel(inputData.CurrentReelIndex);
        }

        private void DeleteScreenshot()
        {
            if (!PhotoDetailInfoController.IsReelUserOwned) return;

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
            viewInstance!.deleteButton.Button.onClick.RemoveListener(ShowDeleteModal);
            HideDeleteModal();

            viewInstance.mainImageCanvasGroup.alpha = 0;

            if (viewInstance.mainImage.texture != null)
                GameObject.Destroy(viewInstance.mainImage.texture);

            viewInstance.mainImage.texture = null;
            PhotoDetailInfoController.Release();
        }

        protected override void OnBeforeViewShow()
        {
            currentReelIndex = inputData.CurrentReelIndex;
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
                    ScreenshotDownloaded?.Invoke();

                    viewInstance!.cameraReelToastMessage?.ShowToastMessage(
                        CameraReelToastMessageType.DOWNLOAD,
                        photoDetailStringMessages.PhotoSuccessfullyDownloadedMessage);
                }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL));
                    viewInstance!.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.FAILURE);
                }
            }

            DownloadAndOpenAsync(downloadScreenshotCts.Token).Forget();
        }

        private void CopyReelLinkClicked()
        {
            ReelCommonActions.CopyReelLink(inputData.AllReels[currentReelIndex].id, decentralandUrlsSource, systemClipboard);
            viewInstance!.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.SUCCESS, photoDetailStringMessages.LinkCopiedMessage);
        }

        private void ShareReelClicked()
        {
            ReelCommonActions.ShareReelToX(photoDetailStringMessages.ShareToXMessage, inputData.AllReels[currentReelIndex].id, decentralandUrlsSource, systemClipboard, webBrowser);
            ScreenshotShared?.Invoke();
        }

        private void ShowPreviousReel()
        {
            currentReelIndex = Math.Clamp(currentReelIndex - 1, 0, inputData.AllReels.Count - 1);
            ShowReel(currentReelIndex);
        }

        private void SetPublicFlag(bool isPublic)
        {
            setPublicCts = setPublicCts.SafeRestart();

            async UniTaskVoid SetPublicFlagAsync(CancellationToken ct)
            {
                try
                {
                    await cameraReelStorageService.UpdateScreenshotVisibilityAsync(inputData.AllReels[currentReelIndex].id,
                        isPublic, ct);
                    inputData.AllReels[currentReelIndex].isPublic = isPublic;
                    viewInstance!.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.SUCCESS,
                        photoDetailStringMessages.PhotoSuccessfullyUpdatedMessage);
                    
                    if(!isPublic)
                        HandleReelSetPrivate();
                }
                catch (OperationCanceledException) { }
                catch (UnityWebRequestException e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL));
                    viewInstance!.cameraReelToastMessage?.ShowToastMessage(CameraReelToastMessageType.FAILURE);
                }
            }

            SetPublicFlagAsync(setPublicCts.Token).Forget();
        }

        private void HandleReelSetPrivate()
        {
            if (inputData.OpenedFrom != PhotoDetailParameter.CallerContext.Passport || inputData.OpenedFromPublicBoard) 
                return;
            
            inputData.ExecuteHideReelFromListAction(currentReelIndex);
            if(inputData.AllReels.Count > 0)
            {
                currentReelIndex %= inputData.AllReels.Count;
                ShowReel(currentReelIndex);
            }
            else
                isClosing = true;
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
            viewInstance.mainImageLoadingSpinner.gameObject.SetActive(true);
            viewInstance.setAsPublicToggle.Toggle.onValueChanged.RemoveListener(SetPublicFlag);
            viewInstance.setAsPublicToggle.SetToggle(inputData.AllReels[currentReelIndex].isPublic);
            viewInstance!.setAsPublicToggle.Toggle.onValueChanged.AddListener(SetPublicFlag);

            if (viewInstance.mainImage.texture != null)
                GameObject.Destroy(viewInstance!.mainImage.texture);

            CameraReelResponseCompact reel = inputData.AllReels[reelIndex];

            UniTask detailInfoTask = PhotoDetailInfoController.ShowPhotoDetailInfoAsync(reel.id, ct);
            Texture2D reelTexture = await cameraReelScreenshotsStorage.GetScreenshotImageAsync(reel.url, false, ct);
            reelTexture.Apply(false, true);
            viewInstance.mainImage.texture = reelTexture;
            aspectRatioFitter.aspectRatio = reelTexture.width * 1f / reelTexture.height;

            viewInstance.mainImageLoadingSpinner.gameObject.SetActive(false);
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

    public struct PhotoDetailStringMessages
    {
        public readonly string ShareToXMessage;
        public readonly string PhotoSuccessfullyDownloadedMessage;
        public readonly string PhotoSuccessfullyUpdatedMessage;
        public readonly string LinkCopiedMessage;

        public PhotoDetailStringMessages(string shareToXMessage, string photoSuccessfullyDownloadedMessage,
            string photoSuccessfullyUpdatedMessage, string linkCopiedMessage)
        {
            ShareToXMessage = shareToXMessage;
            PhotoSuccessfullyDownloadedMessage = photoSuccessfullyDownloadedMessage;
            PhotoSuccessfullyUpdatedMessage = photoSuccessfullyUpdatedMessage;
            LinkCopiedMessage = linkCopiedMessage;
        }
    }
}
