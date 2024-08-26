using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Chat.MessageBus;
using DCL.MapRenderer;
using DCL.Minimap;
using DCL.PlacesAPIService;
using DCL.UI.MainUI;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using Global.Dynamic;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class MinimapPlugin : IDCLGlobalPlugin<MinimapPlugin.MinimapSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly MapRendererContainer mapRendererContainer;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmData realmData;
        private readonly IRealmNavigator realmNavigator;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IScenesCache scenesCache;
        private readonly MainUIView mainUIView;
        private readonly IMapPathEventBus mapPathEventBus;
        private MinimapController minimapController;

        public MinimapPlugin(IMVCManager mvcManager, MapRendererContainer mapRendererContainer, IPlacesAPIService placesAPIService,
            IRealmData realmData, IChatMessagesBus chatMessagesBus, IRealmNavigator realmNavigator, IScenesCache scenesCache, MainUIView mainUIView,
            IMapPathEventBus mapPathEventBus)
        {
            this.mvcManager = mvcManager;
            this.mapRendererContainer = mapRendererContainer;
            this.placesAPIService = placesAPIService;
            this.realmData = realmData;
            this.chatMessagesBus = chatMessagesBus;
            this.realmNavigator = realmNavigator;
            this.scenesCache = scenesCache;
            this.mapPathEventBus = mapPathEventBus;
            this.mainUIView = mainUIView;
        }

        public void Dispose()
        {
            minimapController.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            TrackPlayerPositionSystem? trackPlayerPositionSystem = TrackPlayerPositionSystem.InjectToWorld(ref builder);
            minimapController.HookPlayerPositionTrackingSystem(trackPlayerPositionSystem);
        }

        public async UniTask InitializeAsync(MinimapSettings settings, CancellationToken ct)
        {
            minimapController = new MinimapController(
                () =>
                {
                    MinimapView? view = mainUIView.MinimapView;
                    view.gameObject.SetActive(true);
                    return view;
                },
                mapRendererContainer.MapRenderer, mvcManager, placesAPIService,
                realmData, chatMessagesBus, realmNavigator, scenesCache, mapPathEventBus);

            mvcManager.RegisterController(minimapController);
        }

        public class MinimapSettings : IDCLPluginSettings { }
    }
}
