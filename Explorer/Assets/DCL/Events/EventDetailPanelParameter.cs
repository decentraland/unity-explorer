using DCL.Events;
using DCL.EventsApi;
using DCL.PlacesAPIService;

namespace DCL.Communities.EventInfo
{
    public class EventDetailPanelParameter
    {
        public readonly IEventDTO EventData;
        public readonly PlacesData.PlaceInfo? PlaceData;
        public readonly EventCardView? SummonerEventCard;

        public EventDetailPanelParameter(IEventDTO eventData, PlacesData.PlaceInfo? placeData, EventCardView? summonerPlaceCard = null)
        {
            this.EventData = eventData;
            this.PlaceData = placeData;
            this.SummonerEventCard = summonerPlaceCard;
        }
    }
}
