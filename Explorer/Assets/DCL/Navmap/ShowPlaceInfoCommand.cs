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
            if (VectorUtilities.TryParseVector2Int(placeInfo.base_position, out Vector2Int result))
                //This will trigger a "parcel clicked" event with the data from the parcel
                this.navmapView.SatelliteRenderImage.OnSearchResultParcelSelected(result);

            placesAndEventsPanelController.Toggle(PlacesAndEventsPanelController.Section.PLACE);

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