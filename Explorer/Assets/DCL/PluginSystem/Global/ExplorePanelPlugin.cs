using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.ExplorePanel;
using DCL.Navmap;
using DCL.ParcelsService;
using DCL.PlacesAPIService;
using DCL.Settings;
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
        private readonly ITeleportController teleportController;
        private readonly BackpackSettings backpackSettings;
        private NavmapController navmapController;

        public ExplorePanelPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            MapRendererContainer mapRendererContainer,
            IPlacesAPIService placesAPIService,
            ITeleportController teleportController,
            BackpackSettings backpackSettings)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mapRendererContainer = mapRendererContainer;
            this.placesAPIService = placesAPIService;
            this.teleportController = teleportController;
            this.backpackSettings = backpackSettings;
        }

        public async UniTask InitializeAsync(ExplorePanelSettings settings, CancellationToken ct)
        {
            ExplorePanelView panelView = (await assetsProvisioner.ProvideMainAssetAsync(settings.ExplorePanelPrefab, ct: ct)).Value.GetComponent<ExplorePanelView>();
            ControllerBase<ExplorePanelView, ExplorePanelParameter>.ViewFactoryMethod viewFactoryMethod = ExplorePanelController.Preallocate(panelView, null, out ExplorePanelView explorePanelView);

            navmapController = new NavmapController(navmapView: explorePanelView.GetComponentInChildren<NavmapView>(), mapRendererContainer.MapRenderer, placesAPIService, teleportController);
            await navmapController.InitialiseAssetsAsync(assetsProvisioner, ct);

            SettingsController settingsController = new SettingsController(explorePanelView.GetComponentInChildren<SettingsView>());
            BackpackControler backpackController = new BackpackControler(explorePanelView.GetComponentInChildren<BackpackView>(), backpackSettings.RarityBackgroundsMapping, backpackSettings.CategoryIconsMapping, backpackSettings.RarityColorMappings);

            mvcManager.RegisterController(new ExplorePanelController(viewFactoryMethod, navmapController, settingsController, backpackController));

            mvcManager.RegisterController(new PersistentExplorePanelOpenerController(
                PersistentExplorePanelOpenerController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(settings.PersistentExploreOpenerPrefab, ct: ct)).Value.GetComponent<PersistentExploreOpenerView>(), null), mvcManager)
            );

            //Create here navmap plugin and pass a setting with reference to the navmapview

            mvcManager.ShowAsync(PersistentExplorePanelOpenerController.IssueCommand(new EmptyParameter())).Forget();
        }

        public void Dispose()
        {
            navmapController.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

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
