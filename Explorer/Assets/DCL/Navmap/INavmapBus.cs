using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using System;
using System.Threading;

namespace DCL.Navmap
{
    public interface INavmapBus
    {
        event Action<PlacesData.PlaceInfo> OnPlaceSelected;
        event Action<PlacesData.PlaceInfo> OnJumpIn;
        event Action<PlacesData.PlaceInfo>? OnDestinationSelected;

        void SelectPlace(PlacesData.PlaceInfo place);

        UniTask SearchForPlaceAsync(string place, NavmapSearchPlaceFilter filter, NavmapSearchPlaceSorting sorting, CancellationToken ct);

        UniTask GoBackAsync(CancellationToken ct);

        void ClearHistory();

        void SelectDestination(PlacesData.PlaceInfo place);

        void JumpIn(PlacesData.PlaceInfo place);
    }
}
