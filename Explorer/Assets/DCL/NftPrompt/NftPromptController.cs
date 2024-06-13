using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Input;
using DCL.NftInfoAPIService;
using DCL.UI;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.NftPrompt
{
    public partial class NftPromptController : ControllerBase<NftPromptView, NftPromptController.Params>
    {
        private const string OWNER_NOT_AVAILABLE = "NOT AVAILABLE";
        private const string MULTIPLE_OWNERS_FORMAT = "{0} owners";
        private const int ADDRESS_MAX_CHARS = 11;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly IWebBrowser webBrowser;
        private readonly ICursor cursor;
        private readonly INftMarketAPIClient nftInfoAPIClient;
        private readonly IWebRequestController webRequestController;
        private Action<NftPromptResultType> resultCallback;

        private NftInfo? lastNftInfo;
        private string marketUrl;
        private ImageController placeImageController;
        private CancellationTokenSource cts;

        public NftPromptController(
            ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            ICursor cursor,
            INftMarketAPIClient nftInfoAPIClient,
            IWebRequestController webRequestController) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.cursor = cursor;
            this.nftInfoAPIClient = nftInfoAPIClient;
            this.webRequestController = webRequestController;
        }

        protected override void OnViewInstantiated()
        {
            placeImageController = new ImageController(viewInstance.ImageNft, webRequestController);
            viewInstance.ButtonClose.onClick.AddListener(Dismiss);
            viewInstance.ButtonCancel.onClick.AddListener(Dismiss);
            viewInstance.ButtonOpenMarket.onClick.AddListener(ViewOnMarket);
        }

        protected override void OnViewShow()
        {
            cursor.Unlock();
            RequestNft(inputData.ContractAddress, inputData.TokenId, result =>
            {
                if (result != NftPromptResultType.ViewOnMarket || string.IsNullOrEmpty(marketUrl))
                    return;

                webBrowser.OpenUrl(marketUrl);
            });
        }

        protected override void OnViewClose() =>
            cts.SafeCancelAndDispose();

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance.ButtonClose.OnClickAsync(ct),
                viewInstance.ButtonCancel.OnClickAsync(ct));

        private void RequestNft(string contractAddress, string tokenId, Action<NftPromptResultType> result)
        {
            resultCallback = result;

            if (lastNftInfo != null && lastNftInfo.Value.Equals(contractAddress, tokenId))
            {
                SetNftInfo(lastNftInfo.Value);
                return;
            }

            cts = cts.SafeRestart();
            FetchNftInfoAsync(contractAddress, tokenId, cts.Token).Forget();
        }

        private async UniTask FetchNftInfoAsync(string contractAddress, string tokenId, CancellationToken ct)
        {
            try
            {
                SetLoading();
                var nftInfo = await nftInfoAPIClient.FetchNftInfoAsync(contractAddress, tokenId, ct);
                await UniTask.SwitchToMainThread();
                SetNftInfo(nftInfo);
            }
            catch (OperationCanceledException)
            {}
            catch (Exception)
            {
                ReportHub.LogError(ReportCategory.NFT_INFO_WEB_REQUEST, "OpenExternalUrl: Player is not inside of scene");
                ShowMainErrorFeedback(true);
            }
        }

        private void SetLoading()
        {
            viewInstance.NftContent.SetActive(false);
            ShowImageLoading(false);
            ShowMainLoading(true);
            ShowMainErrorFeedback(false);
        }

        private void ShowImageLoading(bool isVisible)
        {
            if (viewInstance.SpinnerNftImage == null)
                return;

            viewInstance.SpinnerNftImage.SetActive(isVisible);
        }

        private void ShowMainLoading(bool isVisible)
        {
            if (viewInstance.SpinnerGeneral == null)
                return;

            viewInstance.SpinnerGeneral.SetActive(isVisible);
        }

        private void ShowMainErrorFeedback(bool isVisible)
        {
            if (viewInstance.MainErrorFeedbackContent == null)
                return;

            if (isVisible)
                ShowMainLoading(false);

            viewInstance.MainErrorFeedbackContent.SetActive(isVisible);
        }

        private void SetNftInfo(NftInfo info)
        {
            ShowMainLoading(false);
            ShowMainErrorFeedback(false);
            viewInstance.NftContent.SetActive(true);
            viewInstance.TextNftName.text = info.name;

            bool hasMultipleOwners = info.owners.Length > 1;
            if (hasMultipleOwners)
                viewInstance.TextMultipleOwner.text = string.Format(MULTIPLE_OWNERS_FORMAT, info.owners.Length);
            else
            {
                viewInstance.TextOwner.text = info.owners.Length == 1
                    ? FormatOwnerAddress(info.owners[0].address, ADDRESS_MAX_CHARS)
                    : OWNER_NOT_AVAILABLE;
            }

            viewInstance.TextOwner.gameObject.SetActive(!hasMultipleOwners);
            viewInstance.MultipleOwnersContainer.gameObject.SetActive(hasMultipleOwners);

            bool hasDescription = !string.IsNullOrEmpty(info.description);
            if (hasDescription)
                viewInstance.TextDescription.text = info.description;

            viewInstance.ContainerDescription.SetActive(hasDescription);

            viewInstance.TextOpenMarketButton.text = "VIEW";
            if (!string.IsNullOrEmpty(info.marketName))
                viewInstance.TextOpenMarketButton.text = $"{viewInstance.TextOpenMarketButton.text} ON {info.marketName.ToUpper()}";

            marketUrl = null;
            if (!string.IsNullOrEmpty(info.marketLink))
                marketUrl = info.marketLink;
            else if (!string.IsNullOrEmpty(info.assetLink))
                marketUrl = info.assetLink;

            placeImageController.SetImage(null);
            placeImageController.RequestImage(info.imageUrl);

            lastNftInfo = info;
        }

        private string FormatOwnerAddress(string address, int maxCharacters)
        {
            const string ELLIPSIS = "...";

            if (address.Length <= maxCharacters)
                return address;

            int segmentLength = Mathf.FloorToInt((maxCharacters - ELLIPSIS.Length) * 0.5f);
            return $"{address[..segmentLength]}{ELLIPSIS}{address.Substring(address.Length - segmentLength, segmentLength)}";
        }

        private void Dismiss() =>
            resultCallback?.Invoke(NftPromptResultType.Canceled);

        private void ViewOnMarket() =>
            resultCallback?.Invoke(NftPromptResultType.ViewOnMarket);
    }
}
