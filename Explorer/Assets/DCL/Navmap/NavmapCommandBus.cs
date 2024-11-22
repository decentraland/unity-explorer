using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Navmap
{
    public class NavmapCommandBus : INavmapBus
    {
        public delegate INavmapCommand SearchPlaceFactory(
            INavmapBus.SearchPlaceResultDelegate callback,
            INavmapBus.SearchPlaceParams @params);
        public delegate INavmapCommand ShowPlaceInfoFactory(PlacesData.PlaceInfo placeInfo);
        public delegate INavmapCommand ShowEventInfoFactory(EventDTO @event, PlacesData.PlaceInfo? place = null);

        private readonly Stack<INavmapCommand> commands = new ();
        private readonly SearchPlaceFactory searchPlaceFactory;
        private readonly ShowPlaceInfoFactory showPlaceInfoFactory;
        private readonly ShowEventInfoFactory showEventInfoFactory;

        public event Action<PlacesData.PlaceInfo>? OnJumpIn;
        public event Action<PlacesData.PlaceInfo>? OnDestinationSelected;
        public event INavmapBus.SearchPlaceResultDelegate? OnPlaceSearched;
        public event Action<string?>? OnFilterByCategory;

        public NavmapCommandBus(SearchPlaceFactory searchPlaceFactory,
            ShowPlaceInfoFactory showPlaceInfoFactory,
            ShowEventInfoFactory showEventInfoFactory)
        {
            this.searchPlaceFactory = searchPlaceFactory;
            this.showPlaceInfoFactory = showPlaceInfoFactory;
            this.showEventInfoFactory = showEventInfoFactory;
        }

        public async UniTask SelectPlaceAsync(PlacesData.PlaceInfo place, CancellationToken ct)
        {
            INavmapCommand command = showPlaceInfoFactory.Invoke(place);

            await command.ExecuteAsync(ct);

            commands.Push(command);
        }

        public async UniTask SelectEventAsync(EventDTO @event, CancellationToken ct, PlacesData.PlaceInfo? place = null)
        {
            INavmapCommand command = showEventInfoFactory.Invoke(@event, place);

            await command.ExecuteAsync(ct);

            commands.Push(command);
        }

        public async UniTask SearchForPlaceAsync(INavmapBus.SearchPlaceParams @params, CancellationToken ct)
        {
            INavmapCommand command = searchPlaceFactory.Invoke(OnSearchPlacePerformed, @params);

            await command.ExecuteAsync(ct);

            commands.Push(command);
        }

        public async UniTask GoBackAsync(CancellationToken ct)
        {
            if (!commands.TryPop(out INavmapCommand? lastCommand)) return;
            lastCommand.Undo();
            lastCommand.Dispose();

            if (!commands.TryPeek(out var currentCommand)) return;
            await currentCommand.ExecuteAsync(ct);
        }

        public void ClearHistory()
        {
            for (var i = 0; i < commands.Count; i++)
            {
                if (!commands.TryPop(out var command)) continue;
                command.Dispose();
            }
        }

        public void SelectDestination(PlacesData.PlaceInfo place) =>
            OnDestinationSelected?.Invoke(place);

        public void JumpIn(PlacesData.PlaceInfo place) =>
            OnJumpIn?.Invoke(place);

        public void FilterByCategory(string? category) =>
            OnFilterByCategory?.Invoke(category);

        private void OnSearchPlacePerformed(INavmapBus.SearchPlaceParams @params,
            IReadOnlyList<PlacesData.PlaceInfo> places, int totalResultCount) =>
            OnPlaceSearched?.Invoke(@params, places, totalResultCount);
    }
}
