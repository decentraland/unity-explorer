using DCL.EventsApi;
using PlaceInfo = DCL.PlacesAPIService.PlacesData.PlaceInfo;

namespace DCL.Communities.CommunitiesCard.Events
{
    public struct PlaceAndEventDTO
    {
        public PlaceInfo Place;
        public EventWithPlaceIdDTO Event;
    }
}
