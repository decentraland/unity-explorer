using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Navmap
{
    public class SharedNavmapBus : INavmapBus
    {
        private readonly ObjectProxy<INavmapBus> source;

        public SharedNavmapBus(ObjectProxy<INavmapBus> source)
        {
            this.source = source;
            this.source.OnObjectSet += OnObjectSet;
        }

        public event Action<PlacesData.PlaceInfo> OnJumpIn;
        public event Action<PlacesData.PlaceInfo> OnDestinationSelected;
        public event Action<IReadOnlyList<PlacesData.PlaceInfo>> OnPlaceSearched;
        public event Action<string> OnFilterByCategory;

        private void OnObjectSet(INavmapBus obj)
        {
            obj.OnJumpIn += OnJumpIn;
            obj.OnDestinationSelected += OnDestinationSelected;
            obj.OnPlaceSearched += OnPlaceSearched;
            obj.OnFilterByCategory += OnFilterByCategory;
        }

        public UniTask SelectPlaceAsync(PlacesData.PlaceInfo place, CancellationToken ct) =>
            source.Object.SelectPlaceAsync(place, ct);

        public UniTask SelectEventAsync(EventDTO @event, CancellationToken ct, PlacesData.PlaceInfo place = null) =>
            source.Object.SelectEventAsync(@event, ct, place);

        public UniTask SearchForPlaceAsync(CancellationToken ct,
            string? place = null,
            NavmapSearchPlaceFilter filter = NavmapSearchPlaceFilter.All,
            NavmapSearchPlaceSorting sorting = NavmapSearchPlaceSorting.MostActive,
            string? category = null) =>
            source.Object.SearchForPlaceAsync(ct, place, filter, sorting);

        public UniTask GoBackAsync(CancellationToken ct) =>
            source.Object.GoBackAsync(ct);

        public void ClearHistory() =>
            source.Object.ClearHistory();

        public void SelectDestination(PlacesData.PlaceInfo place) =>
            source.Object.SelectDestination(place);

        public void JumpIn(PlacesData.PlaceInfo place) =>
            source.Object.JumpIn(place);

        public void FilterByCategory(string category) =>
            source.Object.FilterByCategory(category);

        public void OnSearchPlacePerformed(IReadOnlyList<PlacesData.PlaceInfo> places) =>
            source.Object.OnSearchPlacePerformed(places);
    }
}
