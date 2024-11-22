using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.Utilities;
using System;
using System.Threading;

namespace DCL.Navmap
{
    public class SharedNavmapBus : INavmapBus
    {
        private readonly ObjectProxy<INavmapBus> source;

        public event Action<PlacesData.PlaceInfo>? OnJumpIn;
        public event Action<PlacesData.PlaceInfo>? OnDestinationSelected;
        public event INavmapBus.SearchPlaceResultDelegate? OnPlaceSearched;
        public event Action<string?>? OnFilterByCategory;

        public SharedNavmapBus(ObjectProxy<INavmapBus> source)
        {
            this.source = source;
            this.source.OnObjectSet += OnObjectSet;
        }

        private void OnObjectSet(INavmapBus obj)
        {
            obj.OnJumpIn += OnJumpIn;
            obj.OnDestinationSelected += OnDestinationSelected;
            obj.OnPlaceSearched += OnPlaceSearched;
            obj.OnFilterByCategory += OnFilterByCategory;
        }

        public async UniTask SelectPlaceAsync(PlacesData.PlaceInfo place, CancellationToken ct)
        {
            if (source.Object == null) return;
            await source.Object.SelectPlaceAsync(place, ct);
        }

        public async UniTask SelectEventAsync(EventDTO @event, CancellationToken ct, PlacesData.PlaceInfo? place = null)
        {
            if (source.Object == null) return;
            await source.Object.SelectEventAsync(@event, ct, place);
        }

        public async UniTask SearchForPlaceAsync(INavmapBus.SearchPlaceParams @params, CancellationToken ct)
        {
            if (source.Object == null) return;
            await source.Object.SearchForPlaceAsync(@params, ct);
        }

        public async UniTask GoBackAsync(CancellationToken ct)
        {
            if (source.Object == null) return;
            await source.Object.GoBackAsync(ct);
        }

        public void ClearHistory()
        {
            source.Object?.ClearHistory();
        }

        public void SelectDestination(PlacesData.PlaceInfo place) =>
            source.Object?.SelectDestination(place);

        public void JumpIn(PlacesData.PlaceInfo place) =>
            source.Object?.JumpIn(place);

        public void FilterByCategory(string? category) =>
            source.Object?.FilterByCategory(category);
    }
}
