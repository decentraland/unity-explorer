using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Chat.MessageBus;
using DCL.Clipboard;
using DCL.Donations;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Minimap;
using DCL.Minimap.Settings;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PlacesAPIService;
using DCL.RealmNavigation;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.UI.SharedSpaceManager;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class MinimapPlugin : IDCLGlobalPlugin<MinimapPlugin.MinimapPluginSettings>
    {
        private readonly MinimapView minimapView;
        private readonly IMapRenderer mapRenderer;
        private readonly IMVCManager mvcManager;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmData realmData;
        private readonly IRealmNavigator realmNavigator;
        private readonly IScenesCache scenesCache;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly Vector2Int startParcelInGenesis;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly ISystemClipboard systemClipboard;
        private readonly IDecentralandUrlsSource decentralandUrls;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly ReloadSceneChatCommand reloadSceneCommand;
        private readonly IRoomHub roomHub;
        private readonly ILoadingStatus loadingStatus;
        private readonly bool includeBannedUsersFromScene;
        private readonly HomePlaceEventBus homePlaceEventBus;
        private readonly IDonationsService donationsService;

        private MinimapController? minimapController;

        public MinimapPlugin(
            MinimapView minimapView,
            IMapRenderer mapRenderer,
            IMVCManager mvcManager,
            IPlacesAPIService placesAPIService,
            IRealmData realmData,
            IRealmNavigator realmNavigator,
            IScenesCache scenesCache,
            IMapPathEventBus mapPathEventBus,
            ISceneRestrictionBusController sceneRestrictionBusController,
            Vector2Int startParcelInGenesis,
            ISharedSpaceManager sharedSpaceManager,
            ISystemClipboard systemClipboard,
            IDecentralandUrlsSource decentralandUrls,
            IChatMessagesBus chatMessagesBus,
            ReloadSceneChatCommand reloadSceneCommand,
            IRoomHub roomHub,
            ILoadingStatus loadingStatus,
            bool includeBannedUsersFromScene,
            HomePlaceEventBus homePlaceEventBus,
            IDonationsService donationsService)
        {
            this.minimapView = minimapView;
            this.mapRenderer = mapRenderer;
            this.mvcManager = mvcManager;
            this.placesAPIService = placesAPIService;
            this.realmData = realmData;
            this.realmNavigator = realmNavigator;
            this.scenesCache = scenesCache;
            this.mapPathEventBus = mapPathEventBus;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.startParcelInGenesis = startParcelInGenesis;
            this.sharedSpaceManager = sharedSpaceManager;
            this.systemClipboard = systemClipboard;
            this.decentralandUrls = decentralandUrls;
            this.chatMessagesBus = chatMessagesBus;
            this.reloadSceneCommand = reloadSceneCommand;
            this.roomHub = roomHub;
            this.loadingStatus = loadingStatus;
            this.includeBannedUsersFromScene = includeBannedUsersFromScene;
            this.homePlaceEventBus = homePlaceEventBus;
            this.donationsService = donationsService;
        }

        public void Dispose() =>
            minimapController?.Dispose();

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            TrackPlayerPositionSystem? trackPlayerPositionSystem = TrackPlayerPositionSystem.InjectToWorld(ref builder);
            minimapController?.HookPlayerPositionTrackingSystem(trackPlayerPositionSystem);
        }

        public async UniTask InitializeAsync(MinimapPluginSettings settings, CancellationToken ct)
        {
            minimapController = new MinimapController(
                minimapView,
                mapRenderer,
                mvcManager,
                placesAPIService,
                realmData,
                realmNavigator,
                scenesCache,
                mapPathEventBus,
                sceneRestrictionBusController,
                startParcelInGenesis,
                sharedSpaceManager,
                systemClipboard,
                decentralandUrls,
                chatMessagesBus,
                reloadSceneCommand,
                roomHub,
                loadingStatus,
                includeBannedUsersFromScene,
                homePlaceEventBus,
                settings.MinimapContextMenuSettings,
                donationsService
            );

            mvcManager.RegisterController(minimapController);
        }

        public class MinimapPluginSettings : IDCLPluginSettings
        {
            [field: SerializeField] public MinimapContextMenuSettings MinimapContextMenuSettings;
        }
    }
}
