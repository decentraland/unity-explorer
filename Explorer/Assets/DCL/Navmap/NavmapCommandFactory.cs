using DCL.EventsApi;
using DCL.PlacesAPIService;

namespace DCL.Navmap
{
    /// <summary>
    ///     Creates the commands executed by <see cref="NavmapCommandBus" />. The navmap UI controllers are attached
    ///     once the explore panel UI is loaded; commands are only created from user interaction with that UI,
    ///     so the controllers are guaranteed to exist by the time a command is built.
    /// </summary>
    public class NavmapCommandFactory
    {
        private readonly IPlacesAPIService placesAPIService;
        private readonly HttpEventsApiService eventsApiService;

        private PlacesAndEventsPanelController? placesAndEventsPanelController;
        private SearchResultPanelController? searchResultPanelController;
        private NavmapSearchBarController? searchBarController;
        private PlaceInfoPanelController? placeInfoPanelController;
        private EventInfoPanelController? eventInfoPanelController;

        public NavmapCommandFactory(IPlacesAPIService placesAPIService, HttpEventsApiService eventsApiService)
        {
            this.placesAPIService = placesAPIService;
            this.eventsApiService = eventsApiService;
        }

        public void AttachUiControllers(
            PlacesAndEventsPanelController placesAndEventsPanelController,
            SearchResultPanelController searchResultPanelController,
            NavmapSearchBarController searchBarController,
            PlaceInfoPanelController placeInfoPanelController,
            EventInfoPanelController eventInfoPanelController)
        {
            this.placesAndEventsPanelController = placesAndEventsPanelController;
            this.searchResultPanelController = searchResultPanelController;
            this.searchBarController = searchBarController;
            this.placeInfoPanelController = placeInfoPanelController;
            this.eventInfoPanelController = eventInfoPanelController;
        }

        public INavmapCommand CreateSearchPlaceCommand(INavmapBus.SearchPlaceResultDelegate callback, INavmapBus.SearchPlaceParams @params) =>
            new SearchForPlaceAndShowResultsCommand(placesAPIService, eventsApiService, placesAndEventsPanelController!,
                searchResultPanelController!, searchBarController!, callback,
                @params);

        public INavmapCommand<AdditionalParams> CreateShowPlaceCommand(PlacesData.PlaceInfo placeInfo) =>
            new ShowPlaceInfoCommand(placeInfo, placeInfoPanelController!, placesAndEventsPanelController!, eventsApiService,
                searchBarController!);

        public INavmapCommand CreateShowEventCommand(EventDTO @event, PlacesData.PlaceInfo? place = null) =>
            new ShowEventInfoCommand(@event, eventInfoPanelController!, placesAndEventsPanelController!,
                searchBarController!, placesAPIService, place);
    }
}
