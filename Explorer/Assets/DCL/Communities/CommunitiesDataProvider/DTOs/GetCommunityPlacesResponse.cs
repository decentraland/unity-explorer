
using DCL.PlacesAPIService;
using System;

namespace DCL.Communities
{
    [Serializable]
    public class GetCommunityPlacesResponse
    {
        public PlacesData.PlaceInfo[] places;
        public int totalPages;
    }
}
