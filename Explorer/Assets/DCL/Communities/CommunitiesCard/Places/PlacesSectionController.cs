using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
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

        private readonly PlacesSectionView view;
        private readonly SectionFetchData<PlaceInfo> placesFetchData = new (PAGE_SIZE);
        private readonly IPlacesAPIService placesAPIService;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly IRealmNavigator realmNavigator;

        protected override SectionFetchData<PlaceInfo> currentSectionFetchData => placesFetchData;

        private CommunityData? communityData = null;
        private bool userCanModify = false;
        private CancellationTokenSource placeCardOperationsCts = new ();

        public PlacesSectionController(PlacesSectionView view,
            IWebRequestController webRequestController,
            IPlacesAPIService placesAPIService,
            WarningNotificationView inWorldWarningNotificationView,
            IRealmNavigator realmNavigator) : base (view, PAGE_SIZE)
        {
            this.view = view;
            this.placesAPIService = placesAPIService;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
            this.realmNavigator = realmNavigator;

            view.InitGrid(() => currentSectionFetchData, webRequestController);

            view.AddPlaceRequested += OnAddPlaceClicked;

            view.ElementLikeToggleChanged += OnElementLikeToggleChanged;
            view.ElementDislikeToggleChanged += OnElementDislikeToggleChanged;
            view.ElementFavoriteToggleChanged += OnElementFavoriteToggleChanged;
            view.ElementShareButtonClicked += OnElementShareButtonClicked;
            view.ElementInfoButtonClicked += OnElementInfoButtonClicked;
            view.ElementJumpInButtonClicked += OnElementJumpInButtonClicked;
        }

        public override void Dispose()
        {
            view.ElementLikeToggleChanged -= OnElementLikeToggleChanged;
            view.ElementDislikeToggleChanged -= OnElementDislikeToggleChanged;
            view.ElementFavoriteToggleChanged -= OnElementFavoriteToggleChanged;
            view.ElementShareButtonClicked -= OnElementShareButtonClicked;
            view.ElementInfoButtonClicked -= OnElementInfoButtonClicked;
            view.ElementJumpInButtonClicked -= OnElementJumpInButtonClicked;

            placeCardOperationsCts.SafeCancelAndDispose();

            base.Dispose();
        }
        
        private void OnAddPlaceClicked()
        {
            throw new NotImplementedException();
            //TODO: open wizard
        }

        private void OnElementJumpInButtonClicked(PlaceInfo placeInfo)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();

            if (!string.IsNullOrWhiteSpace(placeInfo.world_name))
                realmNavigator.TryChangeRealmAsync(URLDomain.FromString(new ENS(placeInfo.world_name).ConvertEnsToWorldUrl()), placeCardOperationsCts.Token).Forget();
            else
                realmNavigator.TeleportToParcelAsync(placeInfo.base_position_processed, placeCardOperationsCts.Token, false).Forget();
        }

        private void OnElementInfoButtonClicked(PlaceInfo obj)
        {
            throw new NotImplementedException();
        }

        private void OnElementShareButtonClicked(PlaceInfo obj)
        {
            throw new NotImplementedException();
        }

        private void OnElementFavoriteToggleChanged(PlaceInfo placeInfo, bool favoriteValue, PlaceCardView placeCardView)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            FavoritePlaceAsync(placeCardOperationsCts.Token).Forget();
            return;

            async UniTaskVoid FavoritePlaceAsync(CancellationToken ct)
            {
                var result = await placesAPIService.SetPlaceFavoriteAsync(placeInfo.id, favoriteValue, ct)
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (!result.Success)
                {
                    placeCardView.SilentlySetFavoriteToggle(!favoriteValue);
                    await inWorldWarningNotificationView.AnimatedShowAsync(FAVORITE_PLACE_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct);
                }
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
                    await inWorldWarningNotificationView.AnimatedShowAsync(DISLIKE_PLACE_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct);

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
                    await inWorldWarningNotificationView.AnimatedShowAsync(LIKE_PLACE_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct);

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
            int offset = (placesFetchData.pageNumber - 1) * PAGE_SIZE;
            int total = communityData!.Value.places.Length;

            int remaining = total - offset;
            int count = Math.Min(PAGE_SIZE, remaining);

            ArraySegment<string> slice = new ArraySegment<string>(communityData.Value.places, offset, count);

            Result<PlacesData.PlacesAPIResponse> response = await placesAPIService.GetPlacesByIdsAsync(slice, ct)
                                                                                  .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!response.Success || !response.Value.ok)
            {
                placesFetchData.pageNumber--;
                return total;
            }

            placesFetchData.members.AddRange(response.Value.data);

            return total;
        }

        public void ShowPlaces(CommunityData community, CancellationToken token)
        {
            cancellationToken = token;

            if (communityData is not null && community.id.Equals(communityData.Value.id)) return;

            communityData = community;
            userCanModify = communityData.Value.role is CommunityMemberRole.moderator or CommunityMemberRole.owner;
            view.SetCanModify(userCanModify);

            FetchNewDataAsync(token).Forget();
        }
    }
}
