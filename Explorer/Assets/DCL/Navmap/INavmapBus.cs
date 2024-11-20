using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Navmap
{
    public interface INavmapBus
    {
        event Action<PlacesData.PlaceInfo> OnJumpIn;
        event Action<PlacesData.PlaceInfo>? OnDestinationSelected;
        event Action<IReadOnlyList<PlacesData.PlaceInfo>>? OnPlaceSearched;
        event Action<string?>? OnFilterByCategory;

        UniTask SelectPlaceAsync(PlacesData.PlaceInfo place, CancellationToken ct);

        UniTask SelectEventAsync(EventDTO @event, CancellationToken ct, PlacesData.PlaceInfo? place = null);

        UniTask SearchForPlaceAsync(NavmapSearchPlaceFilter filter, NavmapSearchPlaceSorting sorting, CancellationToken ct);

        UniTask SearchForPlaceAsync(string place, NavmapSearchPlaceFilter filter, NavmapSearchPlaceSorting sorting, CancellationToken ct);

        UniTask GoBackAsync(CancellationToken ct);

        void ClearHistory();

        void SelectDestination(PlacesData.PlaceInfo place);

        void JumpIn(PlacesData.PlaceInfo place);

        void FilterByCategory(string? category);
    }
}
