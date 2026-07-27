using Cysharp.Threading.Tasks;
using DCL.Input;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.ChangeRealmPrompt
{
    public partial class ChangeRealmPromptController : ControllerBase<ChangeRealmPromptView, ChangeRealmPromptController.Params>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private const string DEFAULT_CONFIRMATION_MESSAGE = "Are you sure you want to enter this World?";

        private const string DEFAULT_CONFIRMATION_MESSAGE = "Are you sure you want to enter this World?";

        private readonly ICursor cursor;
        private readonly Action<string, Vector2Int?> changeRealmCallback;
        private Action<ChangeRealmPromptResultType>? resultCallback;

        public ChangeRealmPromptController(
            ViewFactoryMethod viewFactory,
            ICursor cursor,
            Action<string, Vector2Int?> changeRealmCallback) : base(viewFactory)
        {
            this.cursor = cursor;
            this.changeRealmCallback = changeRealmCallback;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.CloseButton.onClick.AddListener(Dismiss);
            viewInstance.CancelButton.onClick.AddListener(Dismiss);
            viewInstance.ContinueButton.onClick.AddListener(Approve);

            // Message and realm are attacker-controllable (scene changeRealm / deep link). Disable rich-text
            // parsing so neither can inject TMP markup into this consent prompt (SEC-003; same class as SEC-034/050).
            viewInstance.MessageText.richText = false;
            viewInstance.RealmText.richText = false;
        }

        protected override void OnViewShow()
        {
            cursor.Unlock();
            RequestChangeRealm(inputData.Message, inputData.Realm, result =>
            {
                if (result != ChangeRealmPromptResultType.Approved)
                    return;

                changeRealmCallback.Invoke(inputData.Realm, inputData.Position);
            });
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.CloseButton.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct),
                viewInstance.ContinueButton.OnClickAsync(ct));

        private void RequestChangeRealm(string message, string realm, Action<ChangeRealmPromptResultType> result)
        {
            resultCallback = result;
            viewInstance!.MessageText.text = string.IsNullOrEmpty(message) ? DEFAULT_CONFIRMATION_MESSAGE : message;
            viewInstance.RealmText.text = DestinationHostFor(realm);
        }

        /// <summary>
        /// The destination shown to the user: for a URL realm the authority (host[:port]) with any misleading
        /// userinfo (<c>https://trusted@evil.com</c>) and path/query stripped, so the true host is shown — not a
        /// spoof; a world name or realm alias is shown unchanged. This — with rich-text disabled in
        /// <see cref="OnViewInstantiated"/> — is what the user actually consents to.
        /// </summary>
        internal static string DestinationHostFor(string realm)
        {
            int schemeIdx = realm.IndexOf("://", StringComparison.Ordinal);

            if (schemeIdx < 0)
                return realm;

            int start = schemeIdx + 3;
            int end = start;

            while (end < realm.Length && realm[end] != '/' && realm[end] != '?' && realm[end] != '#')
            {
                // Skip past any userinfo (e.g. https://decentraland.org@evil.com) so the real host after the
                // last '@' is displayed, not the trusted-looking prefix (consent-prompt spoofing, SEC-004).
                if (realm[end] == '@')
                    start = end + 1;

                end++;
            }

            return end > start ? realm.Substring(start, end - start) : realm;
        }

        private void Dismiss() =>
            resultCallback?.Invoke(ChangeRealmPromptResultType.Canceled);

        private void Approve() =>
            resultCallback?.Invoke(ChangeRealmPromptResultType.Approved);
    }
}
