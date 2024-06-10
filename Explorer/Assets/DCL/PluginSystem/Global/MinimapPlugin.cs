using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat;
using DCL.Minimap;
using DCL.PlacesAPIService;
using ECS;
using Global.Dynamic;
using MVC;
using System.Threading;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class MinimapPlugin : DCLGlobalPluginBase<MinimapPlugin.MinimapSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly MapRendererContainer mapRendererContainer;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmData realmData;
        private readonly IRealmNavigator realmNavigator;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IScenesCache scenesCache;

        public MinimapPlugin(IAssetsProvisioner assetsProvisioner, IMVCManager mvcManager, MapRendererContainer mapRendererContainer, IPlacesAPIService placesAPIService,
            IRealmData realmData, IChatMessagesBus chatMessagesBus, IRealmNavigator realmNavigator, IScenesCache scenesCache)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mapRendererContainer = mapRendererContainer;
            this.placesAPIService = placesAPIService;
            this.realmData = realmData;
            this.chatMessagesBus = chatMessagesBus;
            this.realmNavigator = realmNavigator;
            this.scenesCache = scenesCache;
        }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(MinimapSettings settings, CancellationToken ct)
        {
            MinimapView? prefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.MinimapPrefab, ct: ct)).Value.GetComponent<MinimapView>();

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> world, in GlobalPluginArguments _) =>
            {
                mvcManager.RegisterController(new MinimapController(MinimapController.CreateLazily(prefab, null),
                    mapRendererContainer.MapRenderer, mvcManager, placesAPIService, TrackPlayerPositionSystem.InjectToWorld(ref world),
                    realmData, chatMessagesBus, realmNavigator, scenesCache));
            };
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public class MinimapSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ExplorePanelPlugin) + "." + nameof(MinimapSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject MinimapPrefab;
        }
    }
}
