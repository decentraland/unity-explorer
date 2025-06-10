using DCL.PlacesAPIService;
using System;

namespace DCL.EventsApi
{
    [Serializable]
    public struct CommunityEventsDTO
    {
        [Serializable]
        public struct PlaceAndEventDTO
        {
            public PlacesData.PlaceInfo place;
            public EventDTO eventData;
        }

        public int totalAmount;
        public PlaceAndEventDTO[] data;
    }
}
