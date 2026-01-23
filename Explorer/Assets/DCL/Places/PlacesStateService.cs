using DCL.PlacesAPIService;
using DCL.Profiles;
using System;
using System.Collections.Generic;

namespace DCL.Places
{
    public class PlacesStateService : IDisposable
    {
        public Dictionary<string, PlaceInfoWithConnectedFriends> CurrentPlaces { get; } = new();

        public class PlaceInfoWithConnectedFriends
        {
            public readonly PlacesData.PlaceInfo PlaceInfo;
            public readonly List<Profile.CompactInfo> ConnectedFriends;

            public PlaceInfoWithConnectedFriends(PlacesData.PlaceInfo placeInfo, List<Profile.CompactInfo> connectedFriends)
            {
                PlaceInfo = placeInfo;
                ConnectedFriends = connectedFriends;
            }
        }

        private List<Profile.CompactInfo> allFriends { get; } = new();

        public PlaceInfoWithConnectedFriends GetPlaceInfoById(string placeId) =>
            CurrentPlaces.GetValueOrDefault(placeId);

        public void AddPlaces(IReadOnlyList<PlacesData.PlaceInfo> places)
        {
            foreach (PlacesData.PlaceInfo place in places)
            {
                List<Profile.CompactInfo> friendsConnectedToPlace = new();
                if (place.connected_addresses != null)
                {
                    foreach (string addressConnected in place.connected_addresses)
                    {
                        if (TryGetFriendById(addressConnected, out Profile.CompactInfo friend))
                            friendsConnectedToPlace.Add(friend);
                    }
                }

                CurrentPlaces[place.id] = new PlaceInfoWithConnectedFriends(place, friendsConnectedToPlace);
            }
        }

        public void ClearPlaces() =>
            CurrentPlaces.Clear();

        public void SetAllFriends(List<Profile.CompactInfo> friends)
        {
            allFriends.Clear();
            allFriends.AddRange(friends);
        }

        public void ClearAllFriends() =>
            allFriends.Clear();

        public void Dispose() =>
            ClearPlaces();

        private bool TryGetFriendById(string userId, out Profile.CompactInfo friendProfile)
        {
            foreach (var friend in allFriends)
            {
                if (!friend.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                    continue;

                friendProfile = friend;
                return true;
            }

            friendProfile = default(Profile.CompactInfo);
            return false;
        }
    }
}
