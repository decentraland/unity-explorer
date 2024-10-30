using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Navmap
{
    public class NavmapCommandBus : INavmapBus
    {
        private readonly Stack<INavmapCommand> commands = new ();
        private readonly Func<string, INavmapCommand> searchPlaceFactory;

        public event Action<PlacesData.PlaceInfo>? OnPlaceSelected;
        public event Action<PlacesData.PlaceInfo>? OnJumpIn;
        public event Action<PlacesData.PlaceInfo>? OnDestinationSelected;

        public NavmapCommandBus(Func<string, INavmapCommand> searchPlaceFactory)
        {
            this.searchPlaceFactory = searchPlaceFactory;
        }

        public void SelectPlace(PlacesData.PlaceInfo place) =>
            OnPlaceSelected?.Invoke(place);

        public async UniTask SearchForPlaceAsync(string place, CancellationToken ct)
        {
            INavmapCommand command = searchPlaceFactory.Invoke(place);

            await command.ExecuteAsync(ct);

            commands.Push(command);
        }

        public async UniTask GoBackAsync(CancellationToken ct)
        {
            if (!commands.TryPop(out INavmapCommand? lastCommand)) return;
            lastCommand.Undo();

            if (!commands.TryPeek(out var currentCommand)) return;
            await currentCommand.ExecuteAsync(ct);
        }

        public void SelectDestination(PlacesData.PlaceInfo place) =>
            OnDestinationSelected?.Invoke(place);

        public void JumpIn(PlacesData.PlaceInfo place) =>
            OnJumpIn?.Invoke(place);
    }
}
