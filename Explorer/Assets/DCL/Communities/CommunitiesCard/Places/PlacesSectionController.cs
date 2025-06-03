using System;
using System.Threading;
using CommunityData = DCL.Communities.GetCommunityResponse.CommunityData;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class PlacesSectionController : IDisposable
    {
        private readonly PlacesSectionView view;

        public PlacesSectionController(PlacesSectionView view)
        {
            this.view = view;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void ShowPlaces(CommunityData communityData, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
