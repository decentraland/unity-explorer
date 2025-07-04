using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.CommunicationData.URLHelpers;
using DCL.Communities.CommunityCreation;
using DCL.Diagnostics;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using Utility;
using Utility.Types;
using CommunityData = DCL.Communities.GetCommunityResponse.CommunityData;
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class PlacesSectionController : CommunityFetchingControllerBase<PlaceInfo, PlacesSectionView>
    {
        private const int PAGE_SIZE = 10;

        private const int WARNING_NOTIFICATION_DURATION_MS = 3000;
        private const string LIKE_PLACE_ERROR_MESSAGE = "There was an error liking the place. Please try again.";
        private const string DISLIKE_PLACE_ERROR_MESSAGE = "There was an error disliking the place. Please try again.";
        private const string FAVORITE_PLACE_ERROR_MESSAGE = "There was an error setting the place as favorite. Please try again.";
        private const string COMMUNITY_PLACES_FETCH_ERROR_MESSAGE = "There was an error fetching the community places. Please try again.";
        private const string COMMUNITY_PLACES_DELETE_ERROR_MESSAGE = "There was an error deleting the community place. Please try again.";

        private const string LINK_COPIED_MESSAGE = "Link copied to clipboard!";

        private const string JUMP_IN_GC_LINK = " https://decentraland.org/jump/?position={0},{1}";
        private const string JUMP_IN_WORLD_LINK = " https://decentraland.org/jump/?realm={0}";
        private const string TWITTER_NEW_POST_LINK = "https://twitter.com/intent/tweet?text={0}&hashtags={1}&url={2}";
        private const string TWITTER_PLACE_DESCRIPTION = "Check out {0}, a cool place I found in Decentraland!";

        private readonly PlacesSectionView view;
        private readonly ICommunitiesDataProvider communitiesDataProvider;
        private readonly SectionFetchData<PlaceInfo> placesFetchData = new (PAGE_SIZE);
        private readonly IPlacesAPIService placesAPIService;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly WarningNotificationView inWorldSuccessNotificationView;
        private readonly IRealmNavigator realmNavigator;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;
        private readonly IMVCManager mvcManager;
        private readonly ObjectProxy<ISpriteCache> spriteCache;

        private string[] communityPlaceIds;

        protected override SectionFetchData<PlaceInfo> currentSectionFetchData => placesFetchData;

        private CommunityData? communityData = null;
        private bool userCanModify = false;
        private CancellationTokenSource placeCardOperationsCts = new ();

        public PlacesSectionController(PlacesSectionView view,
            ObjectProxy<ISpriteCache> placeSpriteCache,
            ICommunitiesDataProvider communitiesDataProvider,
            IPlacesAPIService placesAPIService,
            WarningNotificationView inWorldWarningNotificationView,
            WarningNotificationView inWorldSuccessNotificationView,
            IRealmNavigator realmNavigator,
            IMVCManager mvcManager,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser) : base (view, PAGE_SIZE)
        {
            this.view = view;
            this.communitiesDataProvider = communitiesDataProvider;
            this.placesAPIService = placesAPIService;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
            this.inWorldSuccessNotificationView = inWorldSuccessNotificationView;
            this.realmNavigator = realmNavigator;
            this.mvcManager = mvcManager;
            this.clipboard = clipboard;
            this.webBrowser = webBrowser;
            this.spriteCache = placeSpriteCache;

            view.InitGrid(() => currentSectionFetchData, placeSpriteCache, cancellationToken);

            view.AddPlaceRequested += OnAddPlaceClicked;

            view.ElementLikeToggleChanged += OnElementLikeToggleChanged;
            view.ElementDislikeToggleChanged += OnElementDislikeToggleChanged;
            view.ElementFavoriteToggleChanged += OnElementFavoriteToggleChanged;
            view.ElementShareButtonClicked += OnElementShareButtonClicked;
            view.ElementCopyLinkButtonClicked += OnElementCopyLinkButtonClicked;
            view.ElementInfoButtonClicked += OnElementInfoButtonClicked;
            view.ElementJumpInButtonClicked += OnElementJumpInButtonClicked;
            view.ElementDeleteButtonClicked += OnElementDeleteButtonClicked;
        }

        public override void Dispose()
        {
            view.AddPlaceRequested -= OnAddPlaceClicked;

            view.ElementLikeToggleChanged -= OnElementLikeToggleChanged;
            view.ElementDislikeToggleChanged -= OnElementDislikeToggleChanged;
            view.ElementFavoriteToggleChanged -= OnElementFavoriteToggleChanged;
            view.ElementShareButtonClicked -= OnElementShareButtonClicked;
            view.ElementCopyLinkButtonClicked -= OnElementCopyLinkButtonClicked;
            view.ElementInfoButtonClicked -= OnElementInfoButtonClicked;
            view.ElementJumpInButtonClicked -= OnElementJumpInButtonClicked;
            view.ElementDeleteButtonClicked -= OnElementDeleteButtonClicked;

            placeCardOperationsCts.SafeCancelAndDispose();

            base.Dispose();
        }

        private void OnAddPlaceClicked()
        {
            mvcManager.ShowAsync(
                CommunityCreationEditionController.IssueCommand(new CommunityCreationEditionParameter(
                    canCreateCommunities: true,
                    communityId: communityData!.Value.id,
                    spriteCache.StrictObject)));
        }

        private void OnElementDeleteButtonClicked(PlaceInfo placeInfo)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            DeletePlaceAsync(placeCardOperationsCts.Token).Forget();
            return;

            async UniTaskVoid DeletePlaceAsync(CancellationToken ct)
            {
                var result = await communitiesDataProvider.RemovePlaceFromCommunityAsync(communityData!.Value.id, placeInfo.id, ct)
                                                          .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (!result.Success)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(COMMUNITY_PLACES_DELETE_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                placesFetchData.Items.RemoveAll(elem => elem.id.Equals(placeInfo.id));
                view.RefreshGrid(false);
            }
        }

        private void OnElementJumpInButtonClicked(PlaceInfo placeInfo)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();

            if (!string.IsNullOrWhiteSpace(placeInfo.world_name))
                realmNavigator.TryChangeRealmAsync(URLDomain.FromString(new ENS(placeInfo.world_name).ConvertEnsToWorldUrl()), placeCardOperationsCts.Token).Forget();
            else
                realmNavigator.TeleportToParcelAsync(placeInfo.base_position_processed, placeCardOperationsCts.Token, false).Forget();
        }

        private void OnElementInfoButtonClicked(PlaceInfo place)
        {
            throw new NotImplementedException();
        }

        private void OnElementShareButtonClicked(PlaceInfo place)
        {
            string description = string.Format(TWITTER_PLACE_DESCRIPTION, place.title);
            string twitterLink = string.Format(TWITTER_NEW_POST_LINK, description, "DCLPlace", GetPlaceCopyLink(place));

            webBrowser.OpenUrl(twitterLink);
        }

        private void OnElementCopyLinkButtonClicked(PlaceInfo place)
        {
            clipboard.Set(GetPlaceCopyLink(place));

            inWorldSuccessNotificationView.AnimatedShowAsync(LINK_COPIED_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, cancellationToken)
                                          .SuppressToResultAsync(ReportCategory.COMMUNITIES)
                                          .Forget();
        }

        private static string GetPlaceCopyLink(PlaceInfo place)
        {
            if (!string.IsNullOrEmpty(place.world_name))
                return string.Format(JUMP_IN_WORLD_LINK, place.world_name);

            VectorUtilities.TryParseVector2Int(place.base_position, out var coordinates);
            return string.Format(JUMP_IN_GC_LINK, coordinates.x, coordinates.y);
        }

        private void OnElementFavoriteToggleChanged(PlaceInfo placeInfo, bool favoriteValue, PlaceCardView placeCardView)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            UpdateFavoritePlaceAsync(placeCardOperationsCts.Token).Forget();
            return;

            async UniTaskVoid UpdateFavoritePlaceAsync(CancellationToken ct)
            {
                var result = await placesAPIService.SetPlaceFavoriteAsync(placeInfo.id, favoriteValue, ct)
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (!result.Success)
                {
                    placeCardView.SilentlySetFavoriteToggle(!favoriteValue);
                    await inWorldWarningNotificationView.AnimatedShowAsync(FAVORITE_PLACE_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                }

                placeInfo.user_favorite = favoriteValue;
            }
        }

        private void OnElementDislikeToggleChanged(PlaceInfo placeInfo, bool dislikeValue, PlaceCardView placeCardView)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            DislikePlaceAsync(placeCardOperationsCts.Token).Forget();
            return;

            async UniTaskVoid DislikePlaceAsync(CancellationToken ct)
            {
                var result = await placesAPIService.RatePlaceAsync(dislikeValue ? false : null, placeInfo.id, ct)
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (!result.Success)
                {
                    placeCardView.SilentlySetDislikeToggle(!dislikeValue);
                    await inWorldWarningNotificationView.AnimatedShowAsync(DISLIKE_PLACE_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                    return;
                }

                if (dislikeValue)
                {
                    placeCardView.SilentlySetLikeToggle(false);
                    placeInfo.user_dislike = true;
                    placeInfo.user_like = false;
                }
            }
        }

        private void OnElementLikeToggleChanged(PlaceInfo placeInfo, bool likeValue, PlaceCardView placeCardView)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            LikePlaceAsync(placeCardOperationsCts.Token).Forget();
            return;

            async UniTaskVoid LikePlaceAsync(CancellationToken ct)
            {
                var result = await placesAPIService.RatePlaceAsync(likeValue ? true : null, placeInfo.id, ct)
                                      .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (!result.Success)
                {
                    placeCardView.SilentlySetLikeToggle(!likeValue);
                    await inWorldWarningNotificationView.AnimatedShowAsync(LIKE_PLACE_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                    return;
                }

                if (likeValue)
                {
                    placeCardView.SilentlySetDislikeToggle(false);
                    placeInfo.user_dislike = false;
                    placeInfo.user_like = true;
                }
            }
        }

        public override void Reset()
        {
            communityData = null;
            placesFetchData.Reset();
            view.SetCanModify(false);
            base.Reset();
        }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            int offset = (placesFetchData.PageNumber - 1) * PAGE_SIZE;
            int total = communityPlaceIds.Length;

            int remaining = total - offset;
            int count = Math.Min(PAGE_SIZE, remaining);

            ArraySegment<string> slice = new ArraySegment<string>(communityPlaceIds, offset, count);

            Result<PlacesData.PlacesAPIResponse> response = await placesAPIService.GetPlacesByIdsAsync(slice, ct)
                                                                                  .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!response.Success || !response.Value.ok)
            {
                placesFetchData.PageNumber--;
                await inWorldWarningNotificationView.AnimatedShowAsync(COMMUNITY_PLACES_FETCH_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct)
                                                    .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                return placesFetchData.TotalToFetch;
            }

            placesFetchData.Items.AddRange(response.Value.data);

            return response.Value.total;
        }

        public void ShowPlaces(CommunityData community, string[] placeIds, CancellationToken token)
        {
            cancellationToken = token;

            if (communityData is not null && community.id.Equals(communityData.Value.id)) return;

            communityData = community;
            communityPlaceIds = placeIds;
            userCanModify = communityData.Value.role is CommunityMemberRole.moderator or CommunityMemberRole.owner;
            view.SetCanModify(userCanModify);
            view.SetCommunityData(community);

            FetchNewDataAsync(token).Forget();
        }
    }
}
