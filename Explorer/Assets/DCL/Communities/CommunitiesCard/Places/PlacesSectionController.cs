using System;
using System.Threading;
using CommunityData = DCL.Communities.GetCommunityResponse.CommunityData;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class PlacesSectionController : IDisposable
    {
        private readonly PlacesSectionView view;

        private CancellationToken cancellationToken;
        private bool isFetching;

        public PlacesSectionController(PlacesSectionView view)
        {
            this.view = view;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private void OnNewDataRequested()
        {
            if (isFetching) return;

            FetchNewDataAsync(cancellationToken).Forget();
        }

        public void ShowPlaces(CommunityData communityData, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
