using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.Profiles;
using NBitcoin;
using System;
using System.Collections.Generic;

namespace DCL.Events
{
    public class EventsStateService : IDisposable
    {
        private readonly Dictionary<string, EventDTO> currentEvents = new();
        private readonly Dictionary<string, PlacesData.PlaceInfo> currentPlaces = new();
        private readonly Dictionary<string, Profile.CompactInfo> allFriends = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GetUserCommunitiesData.CommunityData> myCommunities = new(StringComparer.OrdinalIgnoreCase);

        public class EventWithPlaceAndFriendsData
        {
            public EventDTO EventInfo;
            public PlacesData.PlaceInfo? PlaceInfo;
            public List<Profile.CompactInfo> FriendsConnectedToPlace = new();
            public GetUserCommunitiesData.CommunityData? CommunityInfo;
        }

        public EventWithPlaceAndFriendsData? GetEventDataById(string eventId)
        {
            EventWithPlaceAndFriendsData result = new EventWithPlaceAndFriendsData();

            if (currentEvents.TryGetValue(eventId, out EventDTO eventInfo))
            {
                result.EventInfo = eventInfo;

                if (!string.IsNullOrEmpty(eventInfo.place_id))
                {
                    currentPlaces.TryGetValue(eventInfo.place_id, out PlacesData.PlaceInfo? placeInfo);
                    result.PlaceInfo = placeInfo;
                }

                List<Profile.CompactInfo> friendsConnectedToPlace = new();
                if (eventInfo.connected_addresses != null)
                {
                    foreach (string addressConnected in eventInfo.connected_addresses)
                    {
                        if (TryGetFriendById(addressConnected, out Profile.CompactInfo friend))
                            friendsConnectedToPlace.Add(friend);
                    }
                }
                result.FriendsConnectedToPlace = friendsConnectedToPlace;

                if (TryGetCommunityById(eventInfo.community_id, out GetUserCommunitiesData.CommunityData? communityData))
                    result.CommunityInfo = communityData;

                return result;
            }

            return null;
        }

        public void AddEvents(IReadOnlyList<EventDTO> events, bool clearCurrentEvents = false)
        {
            if (clearCurrentEvents)
                ClearEvents();

            foreach (EventDTO eventInfo in events)
                currentEvents.AddOrReplace(eventInfo.id, eventInfo);
        }

        public void AddPlaces(IReadOnlyList<PlacesData.PlaceInfo> places, bool clearCurrentPlaces = false)
        {
            if (clearCurrentPlaces)
                ClearPlaces();

            foreach (PlacesData.PlaceInfo placeInfo in places)
                currentPlaces.AddOrReplace(placeInfo.id, placeInfo);
        }

        public void SetAllFriends(List<Profile.CompactInfo> friends)
        {
            ClearAllFriends();
            foreach (var friend in friends)
                if (!string.IsNullOrEmpty(friend.UserId))
                    allFriends.TryAdd(friend.UserId, friend);
        }

        public void SetMyCommunities(List<GetUserCommunitiesData.CommunityData> myCommunitiesList)
        {
            ClearMyCommunities();
            foreach (var community in myCommunitiesList)
                if (!string.IsNullOrEmpty(community.id))
                    myCommunities.TryAdd(community.id, community);
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

        private bool TryGetFriendById(string userId, out Profile.CompactInfo friendProfile)
        {
            if (string.IsNullOrEmpty(userId))
            {
                friendProfile = default(Profile.CompactInfo);
                return false;
            }

            return allFriends.TryGetValue(userId, out friendProfile);
        }

        private bool TryGetCommunityById(string communityId, out GetUserCommunitiesData.CommunityData? communityData)
        {
            if (string.IsNullOrEmpty(communityId))
            {
                communityData = null;
                return false;
            }

            if (myCommunities.TryGetValue(communityId, out GetUserCommunitiesData.CommunityData community))
            {
                communityData = community;
                return true;
            }

            communityData = null;
            return false;
        }
    }
}
