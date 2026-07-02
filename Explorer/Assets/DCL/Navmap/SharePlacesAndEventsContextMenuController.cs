using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.EventsApi;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.UI;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class SharePlacesAndEventsContextMenuController
    {
        private const string TWITTER_NEW_POST_LINK = "https://twitter.com/intent/tweet?text={0}&hashtags={1}&url={2}";
        private const string TWITTER_PLACE_DESCRIPTION = "Check out {0}, a cool place I found in Decentraland!";

        private readonly SharePlacesAndEventsContextMenuView view;
        private readonly WarningNotificationView warningNotificationView;
        private readonly ISystemClipboard clipboard;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private string? twitterLink;
        private string? copyLink;
        private CancellationTokenSource? showCopyLinkToastCancellationToken;

        public SharePlacesAndEventsContextMenuController(SharePlacesAndEventsContextMenuView view,
            WarningNotificationView warningNotificationView,
            ISystemClipboard clipboard,
            UnityAppWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.view = view;
            this.warningNotificationView = warningNotificationView;
            this.clipboard = clipboard;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
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
            copyLink = string.Format(decentralandUrlsSource.Url(DecentralandUrl.JumpInGenesisCityLink), coordinates.x, coordinates.y);
            var description = string.Format(TWITTER_PLACE_DESCRIPTION, place.title);
            twitterLink = string.Format(TWITTER_NEW_POST_LINK, description, "DCLPlace", copyLink);
        }

        public void Set(EventDTO @event)
        {
            string description = @event.name;

            copyLink = @event.live
                ? string.Format(decentralandUrlsSource.Url(DecentralandUrl.JumpInGenesisCityLink), @event.x, @event.y)
                : string.Format(decentralandUrlsSource.Url(DecentralandUrl.WhatsOnEventLink), @event.id);

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
            webBrowser.OpenUrlMainThreadOnly(twitterLink);
            Hide();
        }
    }
}
