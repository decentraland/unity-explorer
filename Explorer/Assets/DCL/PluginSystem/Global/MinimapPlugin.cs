using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Chat.MessageBus;
using DCL.Minimap;
using DCL.PlacesAPIService;
using DCL.UI.MainUI;
using ECS;
using Global.Dynamic;
using MVC;
using System.Threading;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;

namespace DCL.PluginSystem.Global
{
    public class MinimapPlugin : DCLGlobalPluginBase<MinimapPlugin.MinimapSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly MapRendererContainer mapRendererContainer;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmData realmData;
        private readonly IRealmNavigator realmNavigator;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IScenesCache scenesCache;
        private readonly MainUIContainer mainUIContainer;

        public MinimapPlugin(IMVCManager mvcManager, MapRendererContainer mapRendererContainer, IPlacesAPIService placesAPIService,
            IRealmData realmData, IChatMessagesBus chatMessagesBus, IRealmNavigator realmNavigator, IScenesCache scenesCache, MainUIContainer mainUIContainer)
        {
            this.mvcManager = mvcManager;
            this.mapRendererContainer = mapRendererContainer;
            this.placesAPIService = placesAPIService;
            this.realmData = realmData;
            this.chatMessagesBus = chatMessagesBus;
            this.realmNavigator = realmNavigator;
            this.scenesCache = scenesCache;
            this.mainUIContainer = mainUIContainer;
        }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(MinimapSettings settings, CancellationToken ct)
        {
            return (ref ArchSystemsWorldBuilder<Arch.Core.World> world, in GlobalPluginArguments _) =>
            {
                mvcManager.RegisterController(new MinimapController(
                    () =>
                    {
                        var view = mainUIContainer.MinimapView;
                        view.gameObject.SetActive(true);
                        return view;
                    },
                    mapRendererContainer.MapRenderer, mvcManager, placesAPIService, TrackPlayerPositionSystem.InjectToWorld(ref world),
                    realmData, chatMessagesBus, realmNavigator, scenesCache));
            };
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public class MinimapSettings : IDCLPluginSettings
        {
        }
    }
}
