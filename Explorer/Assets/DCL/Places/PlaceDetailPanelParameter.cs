using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.Profiles;
using System.Collections.Generic;

namespace DCL.Places
{
    public class PlaceDetailPanelParameter
    {
        public readonly PlacesData.PlaceInfo PlaceData;
        public readonly PlaceCardView? SummonerPlaceCard;
        public readonly List<Profile.CompactInfo>? ConnectedFriends;
        public readonly EventDTO? LiveEvent;

        public PlaceDetailPanelParameter(PlacesData.PlaceInfo placeData, PlaceCardView? summonerPlaceCard = null, List<Profile.CompactInfo>? connectedFriends = null, EventDTO? liveEvent = null)
        {
            this.PlaceData = placeData;
            this.SummonerPlaceCard = summonerPlaceCard;
            this.ConnectedFriends = connectedFriends;
            this.LiveEvent = liveEvent;
        }
    }
}
