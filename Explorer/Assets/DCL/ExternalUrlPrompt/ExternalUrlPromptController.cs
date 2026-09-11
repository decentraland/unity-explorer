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
            {
                // Refused by the http(s) scheme policy (SEC-008). Blank the fields so the previous prompt's
                // destination is not left on screen, and drop the callback so the buttons cannot re-approve it.
                resultCallback = null;
                viewInstance!.DomainText.text = string.Empty;
                viewInstance.UrlText.text = string.Empty;
                return;
            }

            Uri uri = inputData.Uri;

            if (IsTrusted(uri))
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
            // Nothing left to consent to: the URL was refused by the scheme policy, or its (scheme, host) is
            // already trusted and was opened in OnViewShow. Either way, close instead of showing the dialog.
            if (inputData.Uri == null || IsTrusted(inputData.Uri))
                return UniTask.CompletedTask;

            return UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct),
                viewInstance.ContinueButton.OnClickAsync(ct));
        }

        public override void Dispose()
        {
            trustedKeys.Clear();
        }

        private bool IsTrusted(Uri uri) =>
            ExternalUrlPolicy.TryGetTrustKey(uri, out string trustKey) && trustedKeys.Contains(trustKey);

        private void RequestOpenUrl(Uri uri, Action<ExternalUrlPromptResultType> result)
        {
            resultCallback = result;
            viewInstance!.DomainText.text = uri.Host;

            // AbsoluteUri, not OriginalString: it is the canonical form UnityAppWebBrowser hands to
            // Application.OpenURL, so the user consents to exactly the string that gets opened, and its
            // percent-escaping is a second barrier against markup smuggled into the raw URL (SEC-008).
            viewInstance.UrlText.text = uri.AbsoluteUri;
            viewInstance.TrustToggle.isOn = false;
        }

        private void Dismiss() =>
            resultCallback?.Invoke(ExternalUrlPromptResultType.Canceled);

        private void Approve() =>
            resultCallback?.Invoke(viewInstance!.TrustToggle.isOn ? ExternalUrlPromptResultType.ApprovedTrusted : ExternalUrlPromptResultType.Approved);
    }
}
