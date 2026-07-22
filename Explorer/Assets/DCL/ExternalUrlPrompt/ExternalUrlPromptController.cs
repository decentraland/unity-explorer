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
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.POPUP;

        private readonly UnityAppWebBrowser webBrowser;
        private readonly ICursor cursor;
        private readonly HashSet<string> trustedKeys = new ();
        private Action<ExternalUrlPromptResultType>? resultCallback;

        public ExternalUrlPromptController(
            ViewFactoryMethod viewFactory,
            UnityAppWebBrowser webBrowser,
            ICursor cursor) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.cursor = cursor;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.CloseButton.onClick.AddListener(Dismiss);
            viewInstance.CancelButton.onClick.AddListener(Dismiss);
            viewInstance.ContinueButton.onClick.AddListener(Approve);
        }

        protected override void OnViewShow()
        {
            if (inputData.Uri == null)
                return;

            Uri uri = inputData.Uri;

            if (ExternalUrlPolicy.TryGetTrustKey(uri, out string trustKey) && trustedKeys.Contains(trustKey))
            {
                webBrowser.OpenUrlMainThreadOnly(uri.OriginalString);
                viewInstance!.CloseButton.OnClickAsync(CancellationToken.None).Forget();
                return;
            }

            cursor.Unlock();
            RequestOpenUrl(uri, result =>
            {
                switch (result)
                {
                    case ExternalUrlPromptResultType.ApprovedTrusted:
                        // Only cache when a real (scheme, host) key exists — empty-host URIs are never trusted (SEC-008).
                        if (ExternalUrlPolicy.TryGetTrustKey(uri, out string key))
                            trustedKeys.Add(key);
                        webBrowser.OpenUrlMainThreadOnly(uri.OriginalString);
                        break;
                    case ExternalUrlPromptResultType.Approved:
                        webBrowser.OpenUrlMainThreadOnly(uri.OriginalString);
                        break;
                }
            });
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (inputData.Uri != null
                && ExternalUrlPolicy.TryGetTrustKey(inputData.Uri, out string trustKey)
                && trustedKeys.Contains(trustKey))
                return UniTask.CompletedTask;

            return UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct),
                viewInstance.ContinueButton.OnClickAsync(ct));
        }

        public override void Dispose()
        {
            trustedKeys.Clear();
        }

        private void RequestOpenUrl(Uri uri, Action<ExternalUrlPromptResultType> result)
        {
            resultCallback = result;
            viewInstance!.DomainText.text = uri.Host;
            viewInstance.UrlText.text = uri.OriginalString;
            viewInstance.TrustToggle.isOn = false;
        }

        private void Dismiss() =>
            resultCallback?.Invoke(ExternalUrlPromptResultType.Canceled);

        private void Approve() =>
            resultCallback?.Invoke(viewInstance!.TrustToggle.isOn ? ExternalUrlPromptResultType.ApprovedTrusted : ExternalUrlPromptResultType.Approved);
    }
}
