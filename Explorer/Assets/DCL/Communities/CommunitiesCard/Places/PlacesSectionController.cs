using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PlacesAPIService;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using System;
using System.Threading;
using Utility.Types;
using CommunityData = DCL.Communities.GetCommunityResponse.CommunityData;
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class PlacesSectionController : CommunityFetchingControllerBase<PlaceInfo, PlacesSectionView>
    {
        private const int PAGE_SIZE = 10;

        private readonly PlacesSectionView view;
        private readonly SectionFetchData<PlaceInfo> placesFetchData = new (PAGE_SIZE);
        private readonly IPlacesAPIService placesAPIService;
        protected override SectionFetchData<PlaceInfo> currentSectionFetchData => placesFetchData;

        private CommunityData? communityData = null;
        private bool userCanModify = false;

        public PlacesSectionController(PlacesSectionView view,
            IWebRequestController webRequestController,
            IPlacesAPIService placesAPIService) : base (view, PAGE_SIZE)
        {
            this.view = view;
            this.placesAPIService = placesAPIService;

            view.InitGrid(() => currentSectionFetchData, webRequestController);

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

            base.Dispose();
        }

        private void OnElementJumpInButtonClicked(PlaceInfo obj)
        {
            throw new NotImplementedException();
        }

        private void OnElementInfoButtonClicked(PlaceInfo obj)
        {
            throw new NotImplementedException();
        }

        private void OnElementShareButtonClicked(PlaceInfo obj)
        {
            throw new NotImplementedException();
        }

        private void OnElementFavoriteToggleChanged(PlaceInfo arg1, bool arg2)
        {
            throw new NotImplementedException();
        }

        private void OnElementDislikeToggleChanged(PlaceInfo arg1, bool arg2)
        {
            throw new NotImplementedException();
        }

        private void OnElementLikeToggleChanged(PlaceInfo arg1, bool arg2)
        {
            throw new NotImplementedException();
        }

        public override void Reset()
        {
            communityData = null;
            placesFetchData.Reset();
            view.SetCanModify(false);
            base.Reset();
        }

        protected override async UniTask FetchNewDataAsync(CancellationToken ct)
        {
            await base.FetchNewDataAsync(ct);

            view.SetEmptyStateActive(placesFetchData.totalToFetch == 0 && !userCanModify);
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
