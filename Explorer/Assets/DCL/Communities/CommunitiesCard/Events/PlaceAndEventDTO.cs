using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;

namespace DCL.Communities.CommunitiesCard.Events
{
    public struct PlaceAndEventDTO
    {
        public PlaceInfo Place;
        public CommunityEventsResponse.CommunityEvent Event;
    }
}
