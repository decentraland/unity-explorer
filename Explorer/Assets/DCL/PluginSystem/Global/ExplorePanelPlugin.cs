using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ExplorePanel;
using DCL.Navmap;
using DCLServices.PlacesAPIService;
using Global.Dynamic;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class ExplorePanelPlugin : IDCLGlobalPlugin<ExplorePanelPlugin.ExplorePanelSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly MapRendererContainer mapRendererContainer;
        private readonly IPlacesAPIService placesAPIService;

        public ExplorePanelPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            MapRendererContainer mapRendererContainer,
            IPlacesAPIService placesAPIService)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mapRendererContainer = mapRendererContainer;
            this.placesAPIService = placesAPIService;
        }

        public async UniTask InitializeAsync(ExplorePanelSettings settings, CancellationToken ct)
        {
            mvcManager.RegisterController(new ExplorePanelController(
                ExplorePanelController.Preallocate(
                    (await assetsProvisioner.ProvideMainAssetAsync(settings.ExplorePanelPrefab, ct: ct)).Value.GetComponent<ExplorePanelView>(), null, out var explorePanelView)));

            mvcManager.RegisterController(new PersistentExplorePanelOpenerController(
                PersistentExplorePanelOpenerController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(settings.PersistentExploreOpenerPrefab, ct: ct)).Value.GetComponent<PersistentExploreOpenerView>(), null), mvcManager)
            );

            NavmapController navmapController = new NavmapController(navmapView: explorePanelView.GetComponentInChildren<NavmapView>(), mapRendererContainer.MapRenderer, placesAPIService, assetsProvisioner);

            mvcManager.ShowAsync(PersistentExplorePanelOpenerController.IssueCommand(new EmptyParameter())).Forget();
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }


        public class ExplorePanelSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ExplorePanelPlugin) + "." + nameof(ExplorePanelSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject ExplorePanelPrefab;

            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject PersistentExploreOpenerPrefab;

            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject MinimapPrefab;
        }
    }
}
