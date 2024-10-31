using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Navmap
{
    public class NavmapCommandBus : INavmapBus
    {
        public delegate INavmapCommand SearchPlaceFactory(string search, NavmapSearchPlaceFilter filter, NavmapSearchPlaceSorting sorting);

        private readonly Stack<INavmapCommand> commands = new ();
        private readonly SearchPlaceFactory searchPlaceFactory;

        public event Action<PlacesData.PlaceInfo>? OnPlaceSelected;
        public event Action<PlacesData.PlaceInfo>? OnJumpIn;
        public event Action<PlacesData.PlaceInfo>? OnDestinationSelected;

        public NavmapCommandBus(SearchPlaceFactory searchPlaceFactory)
        {
            this.searchPlaceFactory = searchPlaceFactory;
        }

        public void SelectPlace(PlacesData.PlaceInfo place) =>
            OnPlaceSelected?.Invoke(place);

        public async UniTask SearchForPlaceAsync(string place, NavmapSearchPlaceFilter filter, NavmapSearchPlaceSorting sorting,
            CancellationToken ct)
        {
            INavmapCommand command = searchPlaceFactory.Invoke(place, filter, sorting);

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
    }
}
