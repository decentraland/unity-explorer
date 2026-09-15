using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.ExplorePanel.Lobby
{
    public sealed partial class LobbyController
    {
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

        private static bool IsAvailable(PlacesData.PlaceInfo place) =>
            !place.disabled && !place.IsEmptyPlace;
    }
}
