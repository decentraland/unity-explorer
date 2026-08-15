using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Utility.Types;
using System;
using System.Collections.Generic;

namespace DCL.Places
{
    public class PlacesStateService : IDisposable
    {
        public Dictionary<PlaceId, PlaceInfoWithConnectedFriends> CurrentPlaces { get; } = new();

        public class PlaceInfoWithConnectedFriends
        {
            public readonly PlacesData.PlaceInfo PlaceInfo;
            public readonly List<Profile.CompactInfo> ConnectedFriends;
            public readonly EventDTO? LiveEvent;

            public PlaceInfoWithConnectedFriends(PlacesData.PlaceInfo placeInfo, List<Profile.CompactInfo> connectedFriends, EventDTO? liveEvent = null)
            {
                PlaceInfo = placeInfo;
                ConnectedFriends = connectedFriends;
                LiveEvent = liveEvent;
            }
        }

        private readonly Dictionary<UserId, Profile.CompactInfo> allFriends = new();
        private readonly Dictionary<PlaceId, EventDTO> liveEvents = new();

        public PlaceInfoWithConnectedFriends? GetPlaceInfoById(PlaceId placeId) =>
            CurrentPlaces.GetValueOrDefault(placeId);

        public void AddPlaces(IReadOnlyList<PlacesData.PlaceInfo> places)
        {
            foreach (PlacesData.PlaceInfo place in places)
            {
                Option<PlaceId> placeId = PlaceId.New(place.id);

                if (!placeId.Has)
                    continue;

                List<Profile.CompactInfo> friendsConnectedToPlace = new();
                if (place.connected_addresses != null)
                {
                    foreach (string addressConnected in place.connected_addresses)
                    {
                        if (TryGetFriendById(addressConnected, out Profile.CompactInfo friend))
                            friendsConnectedToPlace.Add(friend);
                    }
                }

                TryGetLiveEventByPlaceId(placeId.Value, out EventDTO? liveEventAssociatedToPlace);

                CurrentPlaces[placeId.Value] = new PlaceInfoWithConnectedFriends(place, friendsConnectedToPlace, liveEventAssociatedToPlace);
            }
        }

        public void RefreshFriendsData()
        {
            var placeIds = new List<PlaceId>(CurrentPlaces.Keys);
            foreach (PlaceId placeId in placeIds)
            {
                var existing = CurrentPlaces[placeId];
                var place = existing.PlaceInfo;
                List<Profile.CompactInfo> friendsConnectedToPlace = new();
                if (place.connected_addresses != null)
                    foreach (string addr in place.connected_addresses)
                        if (TryGetFriendById(addr, out Profile.CompactInfo friend))
                            friendsConnectedToPlace.Add(friend);

                CurrentPlaces[placeId] = new PlaceInfoWithConnectedFriends(place, friendsConnectedToPlace, existing.LiveEvent);
            }
        }

        public void RefreshLiveEventsData()
        {
            var placeIds = new List<PlaceId>(CurrentPlaces.Keys);
            foreach (PlaceId placeId in placeIds)
            {
                var existing = CurrentPlaces[placeId];
                TryGetLiveEventByPlaceId(placeId, out EventDTO? liveEvent);
                CurrentPlaces[placeId] = new PlaceInfoWithConnectedFriends(existing.PlaceInfo, existing.ConnectedFriends, liveEvent);
            }
        }

        public void ClearPlaces() =>
            CurrentPlaces.Clear();

        public void SetAllFriends(List<Profile.CompactInfo> friends)
        {
            allFriends.Clear();
            foreach (Profile.CompactInfo friend in friends)
                allFriends.TryAdd(friend.UserId, friend);
        }

        public void ClearAllFriends() =>
            allFriends.Clear();

        public void SetLiveEvents(List<EventDTO> events)
        {
            liveEvents.Clear();
            foreach (EventDTO liveEvent in events)
            {
                Option<PlaceId> placeId = PlaceId.New(liveEvent.place_id);

                if (placeId.Has)
                    liveEvents.TryAdd(placeId.Value, liveEvent);
            }
        }

        public void ClearLiveEvents() =>
            liveEvents.Clear();

        public void Dispose() =>
            ClearPlaces();

        private bool TryGetFriendById(string rawUserId, out Profile.CompactInfo friendProfile)
        {
            Option<UserId> userId = UserId.New(rawUserId);

            if (userId.Has)
                return allFriends.TryGetValue(userId.Value, out friendProfile);

            friendProfile = default(Profile.CompactInfo);
            return false;
        }

        private bool TryGetLiveEventByPlaceId(PlaceId placeId, out EventDTO? eventInfo)
        {
            if (liveEvents.TryGetValue(placeId, out EventDTO liveEvent))
            {
                eventInfo = liveEvent;
                return true;
            }

            eventInfo = null;
            return false;
        }
    }
}
