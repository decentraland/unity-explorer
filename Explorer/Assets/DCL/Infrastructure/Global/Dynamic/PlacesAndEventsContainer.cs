using CommunicationData.URLHelpers;
using DCL.EventsApi;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.Navmap;
using DCL.PlacesAPIService;
using DCL.WebRequests;

namespace Global.Dynamic
{
    /// <summary>
    ///     Places/events discovery services and the buses that surface them on the map, minimap and navmap.
    /// </summary>
    public class PlacesAndEventsContainer
    {
        public IPlacesAPIService PlacesAPIService { get; }

        public HttpEventsApiService EventsApiService { get; }

        public IMapPathEventBus MapPathEventBus { get; }

        public INavmapBus NavmapBus { get; }

        /// <summary>
        ///     <see cref="DCL.PluginSystem.Global.ExplorePanelPlugin" /> attaches the navmap UI controllers
        ///     once the explore panel UI is loaded.
        /// </summary>
        public NavmapCommandFactory NavmapCommandFactory { get; }

        public IOnlineUsersProvider OnlineUsersProvider { get; }

        public HomePlaceEventBus HomePlaceEventBus { get; }

        private PlacesAndEventsContainer(
            IPlacesAPIService placesAPIService,
            HttpEventsApiService eventsApiService,
            IMapPathEventBus mapPathEventBus,
            INavmapBus navmapBus,
            NavmapCommandFactory navmapCommandFactory,
            IOnlineUsersProvider onlineUsersProvider,
            HomePlaceEventBus homePlaceEventBus)
        {
            PlacesAPIService = placesAPIService;
            EventsApiService = eventsApiService;
            MapPathEventBus = mapPathEventBus;
            NavmapBus = navmapBus;
            NavmapCommandFactory = navmapCommandFactory;
            OnlineUsersProvider = onlineUsersProvider;
            HomePlaceEventBus = homePlaceEventBus;
        }

        public static PlacesAndEventsContainer Create(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource)
        {
            IOnlineUsersProvider baseUserProvider = new ArchipelagoHttpOnlineUsersProvider(webRequestController,
                URLAddress.FromString(urlsSource.Url(DecentralandUrl.RemotePeers)));

            var onlineUsersProvider = new WorldInfoOnlineUsersProviderDecorator(
                baseUserProvider,
                webRequestController,
                URLAddress.FromString(urlsSource.Url(DecentralandUrl.RemotePeersWorld)));

            var placesAPIService = new PlacesAPIService(new PlacesAPIClient(webRequestController, urlsSource));
            var eventsApiService = new HttpEventsApiService(webRequestController, urlsSource);

            var navmapCommandFactory = new NavmapCommandFactory(placesAPIService, eventsApiService);

            var navmapBus = new NavmapCommandBus(navmapCommandFactory.CreateSearchPlaceCommand,
                navmapCommandFactory.CreateShowPlaceCommand, navmapCommandFactory.CreateShowEventCommand, placesAPIService);

            return new PlacesAndEventsContainer(
                placesAPIService,
                eventsApiService,
                new MapPathEventBus(),
                navmapBus,
                navmapCommandFactory,
                onlineUsersProvider,
                new HomePlaceEventBus());
        }
    }
}
