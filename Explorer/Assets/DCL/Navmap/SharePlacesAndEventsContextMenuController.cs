using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class SharePlacesAndEventsContextMenuController
    {
        private const string JUMP_IN_LINK = "https://play.decentraland.org/?position={0},{1}";
        private const string EVENT_WEBSITE_LINK = "https://decentraland.org/events/event/?id={0}";
        private const string TWITTER_NEW_POST_LINK = "https://twitter.com/intent/tweet?text={0}&hashtags={1}&url={2}";
        private const string TWITTER_PLACE_DESCRIPTION = "Check out {0}, a cool place I found in Decentraland!";

        private readonly SharePlacesAndEventsContextMenuView view;
        private readonly WarningNotificationView warningNotificationView;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;
        private string? twitterLink;
        private string? copyLink;
        private CancellationTokenSource? showCopyLinkToastCancellationToken;

        public SharePlacesAndEventsContextMenuController(SharePlacesAndEventsContextMenuView view,
            WarningNotificationView warningNotificationView,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser)
        {
            this.view = view;
            this.warningNotificationView = warningNotificationView;
            this.clipboard = clipboard;
            this.webBrowser = webBrowser;
            view.ShareOnTwitterButton.onClick.AddListener(ShareOnTwitter);
            view.CopyLinkButton.onClick.AddListener(CopyLink);
            view.CloseButton.onClick.AddListener(Hide);
        }

        public void Show(RectTransform reference)
        {
            view.MenuRoot.SetActive(true);
            view.MenuRoot.transform.position = reference.position;
        }

        public void Hide()
        {
            view.MenuRoot.SetActive(false);
        }

        public void Set(PlacesData.PlaceInfo place)
        {
            VectorUtilities.TryParseVector2Int(place.base_position, out var coordinates);
            copyLink = string.Format(JUMP_IN_LINK, coordinates.x, coordinates.y);
            var description = string.Format(TWITTER_PLACE_DESCRIPTION, place.title);
            twitterLink = string.Format(TWITTER_NEW_POST_LINK, description, "DCLPlace", copyLink);
        }

        public void Set(EventDTO @event)
        {
            string description = @event.name;

            copyLink = @event.live
                ? string.Format(JUMP_IN_LINK, @event.x, @event.y)
                : string.Format(EVENT_WEBSITE_LINK, @event.id);

            twitterLink = string.Format(TWITTER_NEW_POST_LINK, description, "DCLPlace", copyLink);
        }

        private void CopyLink()
        {
            if (string.IsNullOrEmpty(copyLink)) return;
            clipboard.Set(copyLink);

            showCopyLinkToastCancellationToken = showCopyLinkToastCancellationToken.SafeRestart();
            ShowToastAsync(showCopyLinkToastCancellationToken.Token).Forget();
            Hide();

            return;

            async UniTaskVoid ShowToastAsync(CancellationToken ct)
            {
                warningNotificationView.Text.text = "Link copied!";
                warningNotificationView.Show();
                await UniTask.Delay(1000, cancellationToken: ct);
                warningNotificationView.Hide();
            }
        }

        private void ShareOnTwitter()
        {
            if (string.IsNullOrEmpty(twitterLink)) return;
            webBrowser.OpenUrl(twitterLink);
            Hide();
        }
    }
}
