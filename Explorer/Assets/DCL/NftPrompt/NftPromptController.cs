using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Input;
using DCL.NftInfoAPIService;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.NftPrompt
{
    public partial class NftPromptController : ControllerBase<NftPromptView, NftPromptController.Params>
    {
        private const int ADDRESS_MAX_CHARS = 11;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly IWebBrowser webBrowser;
        private readonly ICursor cursor;
        private readonly INftInfoAPIService nftInfoAPIService;
        private Action<NftPromptResultType> resultCallback;

        private NftInfo? lastNftInfo;
        private string marketUrl;
        private CancellationTokenSource cts;

        public NftPromptController(
            ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            ICursor cursor,
            INftInfoAPIService nftInfoAPIService) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.cursor = cursor;
            this.nftInfoAPIService = nftInfoAPIService;
        }

        protected override void OnViewInstantiated()
        {
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
                var nftInfo = await nftInfoAPIService.FetchNftInfoAsync(contractAddress, tokenId, ct);
                await UniTask.SwitchToMainThread();
                SetNftInfo(nftInfo);
            }
            catch (Exception e)
            {
                ShowMainErrorFeedback(true);
            }
        }

        private void SetLoading()
        {
            viewInstance.ImageNft.gameObject.SetActive(false);
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
            viewInstance.TextOwner.text = FormatOwnerAddress(info.owners[0].address, ADDRESS_MAX_CHARS);

            if (!string.IsNullOrEmpty(info.description))
                viewInstance.TextDescription.text = info.description;

            viewInstance.TextOpenMarketButton.text = "VIEW";
            if (!string.IsNullOrEmpty(info.marketName))
                viewInstance.TextOpenMarketButton.text = $"{viewInstance.TextOpenMarketButton.text} ON {info.marketName.ToUpper()}";

            marketUrl = null;
            if (!string.IsNullOrEmpty(info.marketLink))
                marketUrl = info.marketLink;
            else if (!string.IsNullOrEmpty(info.assetLink))
                marketUrl = info.assetLink;

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
