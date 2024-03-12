using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Input;
using MVC;
using System;
using System.Threading;

namespace DCL.NftPrompt
{
    public partial class NftPromptController : ControllerBase<NftPromptView, NftPromptController.Params>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly IWebBrowser webBrowser;
        private readonly ICursor cursor;
        private Action<NftPromptResultType> resultCallback;

        public NftPromptController(
            ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            ICursor cursor) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.cursor = cursor;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.CloseButton.onClick.AddListener(Dismiss);
            viewInstance.CancelButton.onClick.AddListener(Dismiss);
            viewInstance.ViewOnOpenSeaButton.onClick.AddListener(ViewOnOpenSea);
        }

        protected override void OnViewShow()
        {
            cursor.Unlock();
            RequestNft(inputData.ContractAddress, inputData.TokenId, result =>
            {
                if (result != NftPromptResultType.ViewOnOpenSea)
                    return;

                // TODO: Implement the redirection to OpenSea
                //webBrowser.OpenUrl(...);
            });
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct));

        private void RequestNft(string contractAddress, string tokenId, Action<NftPromptResultType> result)
        {
            resultCallback = result;

            // TODO: Implement the NFT request

            // TODO: Fill the UI with the NFT data
            
        }

        private void Dismiss() =>
            resultCallback?.Invoke(NftPromptResultType.Canceled);

        private void ViewOnOpenSea() =>
            resultCallback?.Invoke(NftPromptResultType.ViewOnOpenSea);
    }
}
