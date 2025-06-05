using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Input;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.ExternalUrlPrompt
{
    public partial class ExternalUrlPromptController : ControllerBase<ExternalUrlPromptView, ExternalUrlPromptController.Params>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly IWebBrowser webBrowser;
        private readonly ICursor cursor;
        private readonly List<string> trustedDomains = new ();
        private Action<ExternalUrlPromptResultType> resultCallback;

        public ExternalUrlPromptController(
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
            viewInstance.ContinueButton.onClick.AddListener(Approve);
        }

        protected override void OnViewShow()
        {
            if (inputData.Uri == null)
                return;

            if (trustedDomains.Contains(inputData.Uri.Host))
            {
                webBrowser.OpenUrl(inputData.Uri);
                viewInstance.CloseButton.OnClickAsync(CancellationToken.None).Forget();
                return;
            }

            cursor.Unlock();
            RequestOpenUrl(inputData.Uri, result =>
            {
                switch (result)
                {
                    case ExternalUrlPromptResultType.ApprovedTrusted:
                        if (!trustedDomains.Contains(inputData.Uri.Host))
                            trustedDomains.Add(inputData.Uri.Host);

                        webBrowser.OpenUrl(inputData.Uri);
                        break;
                    case ExternalUrlPromptResultType.Approved:
                        webBrowser.OpenUrl(inputData.Uri);
                        break;
                }
            });
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (inputData.Uri != null && trustedDomains.Contains(inputData.Uri.Host))
                return UniTask.CompletedTask;

            return UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct),
                viewInstance.ContinueButton.OnClickAsync(ct));
        }

        public override void Dispose()
        {
            trustedDomains.Clear();
        }

        private void RequestOpenUrl(Uri uri, Action<ExternalUrlPromptResultType> result)
        {
            resultCallback = result;
            viewInstance.DomainText.text = uri.Host;
            viewInstance.UrlText.text = uri.OriginalString;
            viewInstance.TrustToggle.isOn = false;
        }

        private void Dismiss() =>
            resultCallback?.Invoke(ExternalUrlPromptResultType.Canceled);

        private void Approve() =>
            resultCallback?.Invoke(viewInstance.TrustToggle.isOn ? ExternalUrlPromptResultType.ApprovedTrusted : ExternalUrlPromptResultType.Approved);
    }
}
