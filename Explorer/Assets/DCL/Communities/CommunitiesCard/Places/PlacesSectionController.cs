using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Communities.CommunityCreation;
using DCL.Diagnostics;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Navmap;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Optimization.Pools;
using DCL.Places;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetCommunityResponse.CommunityData;
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;
using PlaceData = DCL.Communities.CommunitiesCard.Places.PlacesSectionController.PlaceData;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class PlacesSectionController : CommunityFetchingControllerBase<PlaceData, PlacesSectionView>
    {
        public struct PlaceData
        {
            public PlaceInfo PlaceInfo;
            public string OwnerName;
        }

        private const int PAGE_SIZE = 10;
        private static readonly ListObjectPool<string> USER_IDS_POOL = new (defaultCapacity: 2);

        private const string COMMUNITY_PLACES_FETCH_ERROR_MESSAGE = "There was an error fetching the community places. Please try again.";
        private const string COMMUNITY_PLACES_DELETE_ERROR_MESSAGE = "There was an error deleting the community place. Please try again.";
        private const string GET_OWNERS_NAMES_ERROR_MESSAGE = "There was an error getting owners names. Please try again.";

        private readonly PlacesSectionView view;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider;
        private readonly SectionFetchData<PlaceData> placesFetchData = new (PAGE_SIZE);
        private readonly IPlacesAPIService placesAPIService;
        private readonly IMVCManager mvcManager;
        private readonly ThumbnailLoader thumbnailLoader;
        private readonly IProfileRepository profileRepository;
        private readonly Dictionary<string, string> userNames = new (StringComparer.OrdinalIgnoreCase);
        private readonly PlacesCardSocialActionsController placesCardSocialActionsController;

        private string[] communityPlaceIds;

        protected override SectionFetchData<PlaceData> currentSectionFetchData => placesFetchData;

        private CommunityData? communityData = null;
        private bool userCanModify = false;
        private CancellationTokenSource placeCardOperationsCts = new ();

        public PlacesSectionController(PlacesSectionView view,
            ThumbnailLoader thumbnailLoader,
            CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider,
            IPlacesAPIService placesAPIService,
            IRealmNavigator realmNavigator,
            IMVCManager mvcManager,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser,
            IProfileRepository profileRepository,
            IDecentralandUrlsSource dclUrlSource,
            HomePlaceEventBus homePlaceEventBus) : base(view, PAGE_SIZE)
        {
            this.view = view;
            view.SetDependencies(homePlaceEventBus);

            this.communitiesDataProvider = communitiesDataProvider;
            this.placesAPIService = placesAPIService;
            this.mvcManager = mvcManager;
            this.thumbnailLoader = thumbnailLoader;
            this.profileRepository = profileRepository;
            this.placesCardSocialActionsController = new PlacesCardSocialActionsController(placesAPIService, realmNavigator, webBrowser, clipboard, dclUrlSource, null, null, homePlaceEventBus);

            view.InitGrid(thumbnailLoader, cancellationToken);

            view.AddPlaceRequested += OnAddPlaceClicked;

            view.ElementLikeToggleChanged += OnElementLikeToggleChanged;
            view.ElementDislikeToggleChanged += OnElementDislikeToggleChanged;
            view.ElementFavoriteToggleChanged += OnElementFavoriteToggleChanged;
            view.ElementHomeToggleChanged += OnElementHomeToggleChanged;
            view.ElementShareButtonClicked += OnElementShareButtonClicked;
            view.ElementCopyLinkButtonClicked += OnElementCopyLinkButtonClicked;
            view.ElementInfoButtonClicked += OnElementInfoButtonClicked;
            view.ElementJumpInButtonClicked += OnElementJumpInButtonClicked;
            view.ElementDeleteButtonClicked += OnElementDeleteButtonClicked;
            view.ElementMainButtonClicked += OnElementMainButtonClicked;
        }

        public override void Dispose()
        {
            view.AddPlaceRequested -= OnAddPlaceClicked;

            view.ElementLikeToggleChanged -= OnElementLikeToggleChanged;
            view.ElementDislikeToggleChanged -= OnElementDislikeToggleChanged;
            view.ElementFavoriteToggleChanged -= OnElementFavoriteToggleChanged;
            view.ElementHomeToggleChanged -= OnElementHomeToggleChanged;
            view.ElementShareButtonClicked -= OnElementShareButtonClicked;
            view.ElementCopyLinkButtonClicked -= OnElementCopyLinkButtonClicked;
            view.ElementInfoButtonClicked -= OnElementInfoButtonClicked;
            view.ElementJumpInButtonClicked -= OnElementJumpInButtonClicked;
            view.ElementDeleteButtonClicked -= OnElementDeleteButtonClicked;
            view.ElementMainButtonClicked -= OnElementMainButtonClicked;

            placeCardOperationsCts.SafeCancelAndDispose();

            base.Dispose();
        }

        private void OnAddPlaceClicked()
        {
            mvcManager.ShowAsync(
                CommunityCreationEditionController.IssueCommand(new CommunityCreationEditionParameter(
                    canCreateCommunities: true,
                    communityId: communityData!.Value.id,
                    thumbnailLoader.Cache!)));
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

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success)
                {
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(COMMUNITY_PLACES_DELETE_ERROR_MESSAGE));
                    return;
                }

                placesFetchData.Items.RemoveAll(elem => elem.PlaceInfo.id.Equals(placeInfo.id));
                RefreshGrid(true);
            }
        }

        private void OnElementMainButtonClicked(PlaceInfo placeInfo, PlaceCardView placeCardView) =>
            mvcManager.ShowAsync(PlaceDetailPanelController.IssueCommand(new PlaceDetailPanelParameter(placeInfo, placeCardView))).Forget();

        private void OnElementJumpInButtonClicked(PlaceInfo placeInfo)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            placesCardSocialActionsController.JumpInPlace(placeInfo, placeCardOperationsCts.Token);
        }

        private void OnElementInfoButtonClicked(PlaceInfo place)
        {
            // The button for this callback is disabled, a user cannot reach this point.
            throw new NotImplementedException();
        }

        private void OnElementShareButtonClicked(PlaceInfo place) =>
            placesCardSocialActionsController.SharePlace(place);

        private void OnElementCopyLinkButtonClicked(PlaceInfo place)
        {
            placesCardSocialActionsController.CopyPlaceLink(place);
        }

        private void OnElementFavoriteToggleChanged(PlaceInfo placeInfo, bool favoriteValue, PlaceCardView placeCardView)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            placesCardSocialActionsController.UpdateFavoritePlaceAsync(placeInfo, favoriteValue, placeCardView, null, placeCardOperationsCts.Token).Forget();
        }

        private void OnElementHomeToggleChanged(PlaceInfo placeInfo, bool homeValue, PlaceCardView placeCardView) =>
            placesCardSocialActionsController.SetPlaceAsHome(placeInfo, homeValue, placeCardView, null);

        private void OnElementDislikeToggleChanged(PlaceInfo placeInfo, bool dislikeValue, PlaceCardView placeCardView)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            placesCardSocialActionsController.DislikePlaceAsync(placeInfo, dislikeValue, placeCardView, null, placeCardOperationsCts.Token).Forget();
        }

        private void OnElementLikeToggleChanged(PlaceInfo placeInfo, bool likeValue, PlaceCardView placeCardView)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            placesCardSocialActionsController.LikePlaceAsync(placeInfo, likeValue, placeCardView, null, placeCardOperationsCts.Token).Forget();
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

            Result<PlacesData.IPlacesAPIResponse> response = await placesAPIService.GetPlacesByIdsAsync(slice, ct)
                                                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!response.Success)
            {
                placesFetchData.PageNumber--;
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(COMMUNITY_PLACES_FETCH_ERROR_MESSAGE));
                return placesFetchData.TotalToFetch;
            }

            using PoolExtensions.Scope<List<string>> userIds = USER_IDS_POOL.AutoScope();
            foreach (var place in response.Value.Data)
               if (!userNames.ContainsKey(place.owner))
                   userIds.Value.Add(place.owner);

            if (userIds.Value.Count > 0)
            {
                List<Profile.CompactInfo> getAvatarsDetailsResult = await profileRepository.GetCompactAsync(userIds.Value, ct);

                if (getAvatarsDetailsResult.Count == 0)
                    NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_OWNERS_NAMES_ERROR_MESSAGE));
                else
                    foreach (Profile.CompactInfo profile in getAvatarsDetailsResult)
                    {
                        userNames.Add(profile.UserId, profile.Name);
                        break;
                    }
            }

            foreach (var place in response.Value.Data)
                placesFetchData.Items.Add(new PlaceData
                {
                    PlaceInfo = place,
                    OwnerName = userNames.GetValueOrDefault(place.owner, string.Empty)
                });

            return response.Value.Total;
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
