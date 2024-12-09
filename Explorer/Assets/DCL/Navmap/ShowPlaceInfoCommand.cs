using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class ShowPlaceInfoCommand : INavmapCommand
    {
        private readonly PlacesData.PlaceInfo placeInfo;
        private readonly NavmapView navmapView;
        private readonly PlaceInfoPanelController placeInfoPanelController;
        private readonly PlacesAndEventsPanelController placesAndEventsPanelController;
        private readonly IEventsApiService eventsApiService;
        private readonly NavmapSearchBarController searchBarController;
        private IReadOnlyList<EventDTO>? events;

        public ShowPlaceInfoCommand(
            PlacesData.PlaceInfo placeInfo,
            NavmapView navmapView,
            PlaceInfoPanelController placeInfoPanelController,
            PlacesAndEventsPanelController placesAndEventsPanelController,
            IEventsApiService eventsApiService,
            NavmapSearchBarController searchBarController)
        {
            this.placeInfo = placeInfo;
            this.navmapView = navmapView;
            this.placeInfoPanelController = placeInfoPanelController;
            this.placesAndEventsPanelController = placesAndEventsPanelController;
            this.eventsApiService = eventsApiService;
            this.searchBarController = searchBarController;
        }

        public void Dispose()
        {
        }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            placesAndEventsPanelController.Toggle(PlacesAndEventsPanelController.Section.PLACE);
            placesAndEventsPanelController.Expand();

            placeInfoPanelController.Set(placeInfo);
            placeInfoPanelController.Toggle(PlaceInfoPanelController.Section.OVERVIEW);
            placeInfoPanelController.HideLiveEvent();
            searchBarController.SetInputText(placeInfo.title);
            searchBarController.Interactable = false;
            searchBarController.EnableBack();
            searchBarController.HideHistoryResults();

            events ??= await eventsApiService.GetEventsByParcelAsync(placeInfo.Positions, ct, true);

            if (events.Count > 0)
                placeInfoPanelController.SetLiveEvent(events[0]);
        }

        public void Undo()
        {
            searchBarController.ClearInput();
            searchBarController.Interactable = true;
            searchBarController.DisableBack();
        }
    }
}
