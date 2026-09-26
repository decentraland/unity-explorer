using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Utility.Types;
using NBitcoin;
using System;
using System.Collections.Generic;

namespace DCL.Events
{
    public class EventsStateService : IDisposable
    {
        private readonly Dictionary<EventId, EventDTO> currentEvents = new();
        private readonly Dictionary<PlaceId, PlacesData.PlaceInfo> currentPlaces = new();
        private readonly Dictionary<UserId, Profile.CompactInfo> allFriends = new();
        private readonly Dictionary<CommunityId, GetUserCommunitiesData.CommunityData> myCommunities = new();

        public class EventWithPlaceAndFriendsData
        {
            public EventDTO EventInfo;
            public PlacesData.PlaceInfo? PlaceInfo;
            public List<Profile.CompactInfo> FriendsConnectedToPlace = new();
            public GetUserCommunitiesData.CommunityData? CommunityInfo;
        }

        public EventWithPlaceAndFriendsData? GetEventDataById(EventId eventId)
        {
            EventWithPlaceAndFriendsData result = new EventWithPlaceAndFriendsData();

            if (currentEvents.TryGetValue(eventId, out EventDTO eventInfo))
            {
                result.EventInfo = eventInfo;

                Option<PlaceId> placeId = PlaceId.New(eventInfo.place_id);

                if (placeId.Has)
                {
                    currentPlaces.TryGetValue(placeId.Value, out PlacesData.PlaceInfo? placeInfo);
                    result.PlaceInfo = placeInfo;
                }

                List<Profile.CompactInfo> friendsConnectedToPlace = new();
                if (eventInfo.connected_addresses != null)
                {
                    foreach (string addressConnected in eventInfo.connected_addresses)
                    {
                        Option<UserId> friendId = UserId.New(addressConnected);

                        if (friendId.Has && allFriends.TryGetValue(friendId.Value, out Profile.CompactInfo friend))
                            friendsConnectedToPlace.Add(friend);
                    }
                }
                result.FriendsConnectedToPlace = friendsConnectedToPlace;

                Option<CommunityId> communityId = CommunityId.New(eventInfo.community_id);

                if (communityId.Has && myCommunities.TryGetValue(communityId.Value, out GetUserCommunitiesData.CommunityData community))
                    result.CommunityInfo = community;

                return result;
            }

            return null;
        }

        public void AddEvents(IReadOnlyList<EventDTO> events, bool clearCurrentEvents = false)
        {
            if (clearCurrentEvents)
                ClearEvents();

            foreach (EventDTO eventInfo in events)
            {
                Option<EventId> eventId = EventId.New(eventInfo.id);

                if (eventId.Has)
                    currentEvents.AddOrReplace(eventId.Value, eventInfo);
            }
        }

        public void AddPlaces(IReadOnlyList<PlacesData.PlaceInfo> places, bool clearCurrentPlaces = false)
        {
            if (clearCurrentPlaces)
                ClearPlaces();

            foreach (PlacesData.PlaceInfo placeInfo in places)
            {
                Option<PlaceId> placeId = PlaceId.New(placeInfo.id);

                if (placeId.Has)
                    currentPlaces.AddOrReplace(placeId.Value, placeInfo);
            }
        }

        public void SetAllFriends(List<Profile.CompactInfo> friends)
        {
            ClearAllFriends();
            foreach (Profile.CompactInfo friend in friends)
                allFriends.TryAdd(friend.UserId, friend);
        }

        public void SetMyCommunities(List<GetUserCommunitiesData.CommunityData> myCommunitiesList)
        {
            ClearMyCommunities();
            foreach (GetUserCommunitiesData.CommunityData community in myCommunitiesList)
            {
                Option<CommunityId> communityId = CommunityId.New(community.id);

                if (communityId.Has)
                    myCommunities.TryAdd(communityId.Value, community);
            }
        }

        public void ClearEvents() =>
            currentEvents.Clear();

        public void ClearPlaces() =>
            currentPlaces.Clear();

        public void ClearAllFriends() =>
            allFriends.Clear();

        public void ClearMyCommunities() =>
            myCommunities.Clear();

        public void Dispose()
        {
            ClearEvents();
            ClearPlaces();
        }
    }
}
