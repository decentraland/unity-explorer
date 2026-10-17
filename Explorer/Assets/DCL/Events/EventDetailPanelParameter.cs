using DCL.Events;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using System;

namespace DCL.Communities.EventInfo
{
    public class EventDetailPanelParameter
    {
        public readonly IEventDTO EventData;
        public readonly PlacesData.PlaceInfo? PlaceData;
        public readonly EventCardView? SummonerEventCard;

        /// <summary>
        ///     When set, Jump in hands the event over to it instead of teleporting right away and the panel closes.
        /// </summary>
        public readonly Action<IEventDTO>? JumpInHandler;

        public EventDetailPanelParameter(IEventDTO eventData, PlacesData.PlaceInfo? placeData, EventCardView? summonerPlaceCard = null, Action<IEventDTO>? jumpInHandler = null)
        {
            this.EventData = eventData;
            this.PlaceData = placeData;
            this.SummonerEventCard = summonerPlaceCard;
            this.JumpInHandler = jumpInHandler;
        }
    }
}
