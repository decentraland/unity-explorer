using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using System.Threading;
using UnityEngine;

namespace DCL.Navmap
{
    public class ShowEventInfoCommand : INavmapCommand
    {
        private readonly EventDTO @event;
        private readonly EventInfoPanelController eventInfoPanelController;
        private readonly PlacesAndEventsPanelController placesAndEventsPanelController;
        private readonly NavmapSearchBarController searchBarController;
        private readonly IPlacesAPIService placesAPIService;
        private PlacesData.PlaceInfo? placeInfo;

        public ShowEventInfoCommand(
            EventDTO @event,
            EventInfoPanelController eventInfoPanelController,
            PlacesAndEventsPanelController placesAndEventsPanelController,
            NavmapSearchBarController searchBarController,
            IPlacesAPIService placesAPIService,
            PlacesData.PlaceInfo? placeInfo = null)
        {
            this.@event = @event;
            this.eventInfoPanelController = eventInfoPanelController;
            this.placesAndEventsPanelController = placesAndEventsPanelController;
            this.searchBarController = searchBarController;
            this.placesAPIService = placesAPIService;
            this.placeInfo = placeInfo;
        }

        public void Dispose()
        {
        }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            placesAndEventsPanelController.Toggle(PlacesAndEventsPanelController.Section.EVENT);

            placeInfo ??= await placesAPIService.GetPlaceAsync(new Vector2Int(@event.coordinates[0], @event.coordinates[1]), ct, true);

            eventInfoPanelController.Set(@event, placeInfo!);
            searchBarController.SetInputText(@event.name);
            searchBarController.Interactable = false;
            searchBarController.EnableBack();
            searchBarController.HideHistoryResults();
        }

        public void Undo()
        {
            searchBarController.ClearInput();
            searchBarController.Interactable = true;
            searchBarController.DisableBack();
        }
    }
}
