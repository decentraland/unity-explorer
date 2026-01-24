using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.MapRenderer;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Navmap;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.Utilities.Extensions;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using Utility;

namespace DCL.Places
{
    public class PlacesCardSocialActionsController
    {
        private const string LIKE_PLACE_ERROR_MESSAGE = "There was an error liking the place. Please try again.";
        private const string DISLIKE_PLACE_ERROR_MESSAGE = "There was an error disliking the place. Please try again.";
        private const string FAVORITE_PLACE_ERROR_MESSAGE = "There was an error setting the place as favorite. Please try again.";
        private const string TWITTER_PLACE_DESCRIPTION = "Check out {0}, a cool place I found in Decentraland!";
        private const string LINK_COPIED_MESSAGE = "Link copied to clipboard!";

        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmNavigator realmNavigator;
        private readonly IWebBrowser webBrowser;
        private readonly ISystemClipboard clipboard;
        private readonly IDecentralandUrlsSource dclUrlSource;
        private readonly INavmapBus? navmapBus;
        private readonly IMapPathEventBus? mapPathEventBus;

        public PlacesCardSocialActionsController(
            IPlacesAPIService placesAPIService,
            IRealmNavigator realmNavigator,
            IWebBrowser webBrowser,
            ISystemClipboard clipboard,
            IDecentralandUrlsSource dclUrlSource,
            INavmapBus? navmapBus,
            IMapPathEventBus? mapPathEventBus)
        {
            this.placesAPIService = placesAPIService;
            this.realmNavigator = realmNavigator;
            this.webBrowser = webBrowser;
            this.clipboard = clipboard;
            this.dclUrlSource = dclUrlSource;
            this.navmapBus = navmapBus;
            this.mapPathEventBus = mapPathEventBus;
        }

        public async UniTaskVoid LikePlaceAsync(PlacesData.PlaceInfo placeInfo, bool likeValue, PlaceCardView? placeCardView, PlaceDetailPanelView? placeDetailPanelView, CancellationToken ct)
        {
            var result = await placesAPIService.RatePlaceAsync(likeValue ? true : null, placeInfo.id, ct)
                                               .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                placeCardView?.SilentlySetLikeToggle(!likeValue);
                placeDetailPanelView?.SilentlySetLikeToggle(!likeValue);
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(LIKE_PLACE_ERROR_MESSAGE));
                return;
            }

            if (likeValue)
            {
                placeCardView?.SilentlySetDislikeToggle(false);
                placeDetailPanelView?.SilentlySetDislikeToggle(false);
                placeInfo.user_dislike = false;
                placeInfo.user_like = true;
            }

            placeCardView?.SilentlySetLikeToggle(likeValue);
            placeDetailPanelView?.SilentlySetLikeToggle(likeValue);
        }

        public async UniTaskVoid DislikePlaceAsync(PlacesData.PlaceInfo placeInfo, bool dislikeValue, PlaceCardView? placeCardView, PlaceDetailPanelView? placeDetailPanelView, CancellationToken ct)
        {
            var result = await placesAPIService.RatePlaceAsync(dislikeValue ? false : null, placeInfo.id, ct)
                                               .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                placeCardView?.SilentlySetDislikeToggle(!dislikeValue);
                placeDetailPanelView?.SilentlySetDislikeToggle(!dislikeValue);
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(DISLIKE_PLACE_ERROR_MESSAGE));
                return;
            }

            if (dislikeValue)
            {
                placeCardView?.SilentlySetLikeToggle(false);
                placeDetailPanelView?.SilentlySetLikeToggle(false);
                placeInfo.user_dislike = true;
                placeInfo.user_like = false;
            }

            placeCardView?.SilentlySetDislikeToggle(dislikeValue);
            placeDetailPanelView?.SilentlySetDislikeToggle(dislikeValue);
        }

        public async UniTaskVoid UpdateFavoritePlaceAsync(PlacesData.PlaceInfo placeInfo, bool favoriteValue, PlaceCardView? placeCardView, PlaceDetailPanelView? placeDetailPanelView, CancellationToken ct)
        {
            var result = await placesAPIService.SetPlaceFavoriteAsync(placeInfo.id, favoriteValue, ct)
                                               .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                placeCardView?.SilentlySetFavoriteToggle(!favoriteValue);
                placeDetailPanelView?.SilentlySetFavoriteToggle(!favoriteValue);
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(FAVORITE_PLACE_ERROR_MESSAGE));
            }

            placeInfo.user_favorite = favoriteValue;
            placeCardView?.SilentlySetFavoriteToggle(favoriteValue);
            placeDetailPanelView?.SilentlySetFavoriteToggle(favoriteValue);
        }

        public void JumpInPlace(PlacesData.PlaceInfo placeInfo, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(placeInfo.world_name))
                realmNavigator.TryChangeRealmAsync(URLDomain.FromString(new ENS(placeInfo.world_name).ConvertEnsToWorldUrl()), ct).Forget();
            else
                realmNavigator.TeleportToParcelAsync(placeInfo.base_position_processed, ct, false).Forget();
        }

        public void SharePlace(PlacesData.PlaceInfo placeInfo)
        {
            var description = string.Format(TWITTER_PLACE_DESCRIPTION, placeInfo.title);
            var twitterLink = string.Format(dclUrlSource.Url(DecentralandUrl.TwitterNewPostLink), description, "DCLPlace", GetPlaceCopyLink(placeInfo));

            webBrowser.OpenUrl(twitterLink);
        }

        public void CopyPlaceLink(PlacesData.PlaceInfo placeInfo)
        {
            clipboard.Set(GetPlaceCopyLink(placeInfo));

            NotificationsBusController.Instance.AddNotification(new DefaultSuccessNotification(LINK_COPIED_MESSAGE));
        }

        private string GetPlaceCopyLink(PlacesData.PlaceInfo place)
        {
            if (!string.IsNullOrEmpty(place.world_name))
                return string.Format(dclUrlSource.Url(DecentralandUrl.JumpInWorldLink), place.world_name);

            VectorUtilities.TryParseVector2Int(place.base_position, out var coordinates);

            return string.Format(dclUrlSource.Url(DecentralandUrl.JumpInGenesisCityLink), coordinates.x, coordinates.y);
        }

        public void StartNavigationToPlace(PlacesData.PlaceInfo placeInfo) =>
            navmapBus?.SelectDestination(placeInfo);

        public void ExitNavigationToPlace() =>
            mapPathEventBus?.RemoveDestination();
    }
}
