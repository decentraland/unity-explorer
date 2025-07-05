using DCL.EventsApi;
using DCL.PlacesAPIService;

namespace DCL.Communities.EventInfo
{
    public class EventInfoParameter
    {
        public readonly IEventDTO EventData;
        public readonly PlacesData.PlaceInfo PlaceData;

        public EventInfoParameter(IEventDTO eventData, PlacesData.PlaceInfo placeData)
        {
            this.EventData = eventData;
            this.PlaceData = placeData;
        }
    }
}
