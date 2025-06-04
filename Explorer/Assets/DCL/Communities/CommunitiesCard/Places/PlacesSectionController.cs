using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using CommunityData = DCL.Communities.GetCommunityResponse.CommunityData;
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class PlacesSectionController : CommunityFetchingControllerBase<PlaceInfo, PlacesSectionView>
    {
        private const int PAGE_SIZE = 10;

        private readonly PlacesSectionView view;
        private readonly SectionFetchData<PlaceInfo> placesFetchData = new (PAGE_SIZE);
        protected override SectionFetchData<PlaceInfo> currentSectionFetchData => placesFetchData;

        private CommunityData? communityData = null;
        private bool userCanModify = false;

        public PlacesSectionController(PlacesSectionView view)
        : base (view, PAGE_SIZE)
        {
            this.view = view;

            view.InitGrid(() => currentSectionFetchData);

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

        public void Reset()
        {
            communityData = null;

            placesFetchData.Reset();

            isFetching = false;
        }

        protected override async UniTask FetchNewDataAsync(CancellationToken ct)
        {
            await base.FetchNewDataAsync(ct);

            view.SetEmptyStateActive(placesFetchData.totalToFetch == 0 && !userCanModify);
        }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            return 0;
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
