using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.Friends;
using DCL.Multiplayer.Connectivity;
using DCL.PlacesAPIService;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.ExplorePanel.Lobby
{
    public sealed partial class LobbyController
    {
        private const int PAGE_SIZE = 50;
        private async UniTask<IReadOnlyList<PlacesData.PlaceInfo>> LoadPlacesAsync(CancellationToken ct)
        {
            using PlacesData.IPlacesAPIResponse response = await placesApi.SearchDestinationsAsync(0, 24, ct,
                withConnectedUsers: true, onlySdk7: false, withLiveEvents: true);
            var featured = new List<PlacesData.PlaceInfo>(response.Data.Count);
            foreach (PlacesData.PlaceInfo place in response.Data)
                if (IsAvailable(place)) featured.Add(place);

            featured.Sort(static (a, b) =>
            {
                int featuredOrder = (b.featured || b.highlighted).CompareTo(a.featured || a.highlighted);
                return featuredOrder != 0 ? featuredOrder : b.user_count.CompareTo(a.user_count);
            });
            return featured;
        }

        private async UniTask<IReadOnlyList<PlacesData.PlaceInfo>> LoadReturnPlacesAsync(CancellationToken ct)
        {
            var result = new List<PlacesData.PlaceInfo>(12);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var recentIds = new List<string>(placesApi.GetRecentlyVisitedPlaces());
            if (recentIds.Count > 0)
            {
                using PlacesData.IPlacesAPIResponse recent = await placesApi.GetDestinationsByIdsAsync(recentIds, ct, renewCache: true, withConnectedUsers: true);
                foreach (string id in recentIds)
                    foreach (PlacesData.PlaceInfo place in recent.Data)
                        if (place.id == id && IsAvailable(place) && seen.Add(place.id)) result.Add(place);
            }

            using PlacesData.IPlacesAPIResponse favorites = await placesApi.GetFavoritesDestinationsAsync(ct, 0, 12, withConnectedUsers: true);
            foreach (PlacesData.PlaceInfo place in favorites.Data)
                if (IsAvailable(place) && seen.Add(place.id)) result.Add(place);

            if (result.Count > 12) result.RemoveRange(12, result.Count - 12);
            return result;
        }

        private async UniTask<IReadOnlyList<EventDTO>> LoadEventsAsync(CancellationToken ct)
        {
            IReadOnlyList<EventDTO> response = await eventsApi.GetEventsAsync(ct, onlyLiveEvents: true, withConnectedUsers: true);
            var liveEvents = new List<EventDTO>(response.Count);
            foreach (EventDTO liveEvent in response)
                if (liveEvent.live) liveEvents.Add(liveEvent);
            return liveEvents;
        }

        private async UniTask<IReadOnlyList<(Profile.CompactInfo Profile, string PlaceName)>> LoadFriendsAsync(CancellationToken ct)
        {
            var result = new List<(Profile.CompactInfo Profile, string PlaceName)>();
            if (friendsService == null) return result;
            var profiles = new Dictionary<string, Profile.CompactInfo>(StringComparer.OrdinalIgnoreCase);
            for (int page = 0; ; page++)
            {
                using PaginatedFriendsResult response = await friendsService.GetFriendsAsync(page, PAGE_SIZE, ct);
                int before = profiles.Count;
                foreach (Profile.CompactInfo profile in response.Friends) profiles[profile.UserId.Value] = profile;
                if (response.Friends.Count == 0 || profiles.Count == before || profiles.Count >= response.TotalAmount) break;
            }

            if (profiles.Count == 0) return result;

            IReadOnlyCollection<OnlineUserData> locations = await onlineUsers.GetAsync(profiles.Keys, ct);
            foreach (OnlineUserData location in locations)
            {
                if (!profiles.TryGetValue(location.avatarId, out Profile.CompactInfo profile)) continue;
                Vector2Int parcel = new (Mathf.FloorToInt(location.position.x / 16), Mathf.FloorToInt(location.position.z / 16));
                string placeName = location.IsInWorld ? location.worldName ?? "World" : $"Genesis City ({parcel.x}, {parcel.y})";
                PlacesData.PlaceInfo? place = location.IsInWorld
                    ? await placesApi.GetWorldAsync(parcel, location.worldName ?? string.Empty, ct)
                    : await placesApi.GetPlaceAsync(parcel, ct);
                if (place != null && !place.IsEmptyPlace) placeName = place.title;
                result.Add((profile, placeName));
            }

            return result;
        }

        private static bool IsAvailable(PlacesData.PlaceInfo place) =>
            !place.disabled && !place.IsEmptyPlace;
    }
}
