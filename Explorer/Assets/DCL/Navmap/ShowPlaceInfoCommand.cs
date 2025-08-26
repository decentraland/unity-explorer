using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Navmap
{
    public class ShowPlaceInfoCommand : INavmapCommand<AdditionalParams>
    {
        private readonly PlacesData.PlaceInfo placeInfo;
        private readonly PlaceInfoPanelController placeInfoPanelController;
        private readonly PlacesAndEventsPanelController placesAndEventsPanelController;
        private readonly IEventsApiService eventsApiService;
        private readonly NavmapSearchBarController searchBarController;
        private IReadOnlyList<EventDTO>? events;

        public ShowPlaceInfoCommand(
            PlacesData.PlaceInfo placeInfo,
            PlaceInfoPanelController placeInfoPanelController,
            PlacesAndEventsPanelController placesAndEventsPanelController,
            IEventsApiService eventsApiService,
            NavmapSearchBarController searchBarController)
        {
            this.placeInfo = placeInfo;
            this.placeInfoPanelController = placeInfoPanelController;
            this.placesAndEventsPanelController = placesAndEventsPanelController;
            this.eventsApiService = eventsApiService;
            this.searchBarController = searchBarController;
        }

        public void Dispose()
        {
        }

        public async UniTask ExecuteAsync(AdditionalParams? additionalParams, CancellationToken ct)
        {
            placesAndEventsPanelController.Toggle(PlacesAndEventsPanelController.Section.PLACE);
            placesAndEventsPanelController.Expand();

            placeInfoPanelController.Set(placeInfo);
            placeInfoPanelController.SetOriginParcel(additionalParams?.OriginalParcel);
            placeInfoPanelController.Toggle(PlaceInfoPanelController.Section.OVERVIEW);
            placeInfoPanelController.HideLiveEvent();
            searchBarController.ToggleClearButton(true);
            searchBarController.SetInputText(placeInfo.title);
            searchBarController.Interactable = false;

            if(additionalParams is { IsFromSearchResults: true })
                searchBarController.EnableBack();
            else
                searchBarController.DisableBack();

            events ??= await eventsApiService.GetEventsByParcelAsync(placeInfo.Positions, ct, true);

            if (events.Count > 0)
                placeInfoPanelController.SetLiveEvent(events[0]);
        }

        public UniTask ExecuteAsync(CancellationToken ct) =>
            ExecuteAsync(null, ct);

        public void Undo()
        {
            searchBarController.Interactable = true;
            searchBarController.DisableBack();
        }
    }
}
