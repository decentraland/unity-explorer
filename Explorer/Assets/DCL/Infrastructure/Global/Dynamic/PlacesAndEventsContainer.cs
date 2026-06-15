using CommunicationData.URLHelpers;
using DCL.EventsApi;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.Navmap;
using DCL.PlacesAPIService;
using DCL.Utilities;
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

        /// <summary>
        ///     Filled by <see cref="DCL.PluginSystem.Global.ExplorePanelPlugin" /> once the navmap controller exists.
        /// </summary>
        public ObjectProxy<INavmapBus> ExplorePanelNavmapBus { get; }

        public INavmapBus SharedNavmapBus { get; }

        public IOnlineUsersProvider OnlineUsersProvider { get; }

        public HomePlaceEventBus HomePlaceEventBus { get; }

        private PlacesAndEventsContainer(
            IPlacesAPIService placesAPIService,
            HttpEventsApiService eventsApiService,
            IMapPathEventBus mapPathEventBus,
            ObjectProxy<INavmapBus> explorePanelNavmapBus,
            INavmapBus sharedNavmapBus,
            IOnlineUsersProvider onlineUsersProvider,
            HomePlaceEventBus homePlaceEventBus)
        {
            PlacesAPIService = placesAPIService;
            EventsApiService = eventsApiService;
            MapPathEventBus = mapPathEventBus;
            ExplorePanelNavmapBus = explorePanelNavmapBus;
            SharedNavmapBus = sharedNavmapBus;
            OnlineUsersProvider = onlineUsersProvider;
            HomePlaceEventBus = homePlaceEventBus;
        }

        public static PlacesAndEventsContainer Create(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource)
        {
            var explorePanelNavmapBus = new ObjectProxy<INavmapBus>();

            IOnlineUsersProvider baseUserProvider = new ArchipelagoHttpOnlineUsersProvider(webRequestController,
                URLAddress.FromString(urlsSource.Url(DecentralandUrl.RemotePeers)));

            var onlineUsersProvider = new WorldInfoOnlineUsersProviderDecorator(
                baseUserProvider,
                webRequestController,
                URLAddress.FromString(urlsSource.Url(DecentralandUrl.RemotePeersWorld)));

            return new PlacesAndEventsContainer(
                new PlacesAPIService(new PlacesAPIClient(webRequestController, urlsSource)),
                new HttpEventsApiService(webRequestController, urlsSource),
                new MapPathEventBus(),
                explorePanelNavmapBus,
                new SharedNavmapBus(explorePanelNavmapBus),
                onlineUsersProvider,
                new HomePlaceEventBus());
        }
    }
}
