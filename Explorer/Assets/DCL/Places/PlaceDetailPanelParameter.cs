using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.Profiles;
using System;
using System.Collections.Generic;

namespace DCL.Places
{
    public class PlaceDetailPanelParameter
    {
        public readonly PlacesData.PlaceInfo PlaceData;
        public readonly PlaceCardView? SummonerPlaceCard;
        public readonly List<Profile.CompactInfo>? ConnectedFriends;
        public readonly EventDTO? LiveEvent;

        /// <summary>
        ///     When set, Jump in hands the place over to it instead of teleporting right away and the panel closes.
        /// </summary>
        public readonly Action<PlacesData.PlaceInfo>? JumpInHandler;

        public PlaceDetailPanelParameter(PlacesData.PlaceInfo placeData, PlaceCardView? summonerPlaceCard = null, List<Profile.CompactInfo>? connectedFriends = null, EventDTO? liveEvent = null,
            Action<PlacesData.PlaceInfo>? jumpInHandler = null)
        {
            this.PlaceData = placeData;
            this.SummonerPlaceCard = summonerPlaceCard;
            this.ConnectedFriends = connectedFriends;
            this.LiveEvent = liveEvent;
            this.JumpInHandler = jumpInHandler;
        }
    }
}
