using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using DCL.ExplorePanel;
using DCL.Navmap;
using DCL.ParcelsService;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Settings;
using DCL.UI;
using DCL.Web3Authentication.Identities;
using DCL.WebRequests;
using Global.Dynamic;
using MVC;
using System;
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
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackEventBus backpackEventBus;
        private readonly IWebRequestController webRequestController;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWearableCatalog wearableCatalog;
        private NavmapController navmapController;
        private BackpackControler backpackController;

        public ExplorePanelPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            MapRendererContainer mapRendererContainer,
            IPlacesAPIService placesAPIService,
            ITeleportController teleportController,
            BackpackSettings backpackSettings,
            BackpackCommandBus backpackCommandBus,
            BackpackEventBus backpackEventBus,
            IWebRequestController webRequestController,
            IWeb3IdentityCache web3IdentityCache,
            IWearableCatalog wearableCatalog)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mapRendererContainer = mapRendererContainer;
            this.placesAPIService = placesAPIService;
            this.teleportController = teleportController;
            this.backpackSettings = backpackSettings;
            this.backpackCommandBus = backpackCommandBus;
            this.backpackEventBus = backpackEventBus;
            this.webRequestController = webRequestController;
            this.web3IdentityCache = web3IdentityCache;
            this.wearableCatalog = wearableCatalog;
        }

        public async UniTask InitializeAsync(ExplorePanelSettings settings, CancellationToken ct)
        {
            ExplorePanelView panelView = (await assetsProvisioner.ProvideMainAssetAsync(settings.ExplorePanelPrefab, ct: ct)).Value.GetComponent<ExplorePanelView>();
            ControllerBase<ExplorePanelView, ExplorePanelParameter>.ViewFactoryMethod viewFactoryMethod = ExplorePanelController.Preallocate(panelView, null, out ExplorePanelView explorePanelView);

            navmapController = new NavmapController(navmapView: explorePanelView.GetComponentInChildren<NavmapView>(), mapRendererContainer.MapRenderer, placesAPIService, teleportController, webRequestController);
            await navmapController.InitialiseAssetsAsync(assetsProvisioner, ct);

            (ProvidedAsset<NFTColorsSO> rarityColorMappings, ProvidedAsset<NftTypeIconSO> categoryIconsMapping, ProvidedAsset<NftTypeIconSO> rarityBackgroundsMapping, ProvidedAsset<NftTypeIconSO> rarityInfoPanelBackgroundsMapping) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetAsync(backpackSettings.RarityColorMappings, ct),
                assetsProvisioner.ProvideMainAssetAsync(backpackSettings.CategoryIconsMapping, ct),
                assetsProvisioner.ProvideMainAssetAsync(backpackSettings.RarityBackgroundsMapping, ct),
                assetsProvisioner.ProvideMainAssetAsync(backpackSettings.RarityInfoPanelBackgroundsMapping, ct));

            SettingsController settingsController = new SettingsController(explorePanelView.GetComponentInChildren<SettingsView>());
            PageButtonView pageButtonView = (await assetsProvisioner.ProvideMainAssetAsync(backpackSettings.PageButtonView, ct)).Value.GetComponent<PageButtonView>();
            backpackController = new BackpackControler(explorePanelView.GetComponentInChildren<BackpackView>(), rarityBackgroundsMapping.Value, rarityInfoPanelBackgroundsMapping.Value, categoryIconsMapping.Value, rarityColorMappings.Value, backpackCommandBus, backpackEventBus, web3IdentityCache, wearableCatalog, pageButtonView);
            await backpackController.InitialiseAssetsAsync(assetsProvisioner, ct);

            mvcManager.RegisterController(new ExplorePanelController(viewFactoryMethod, navmapController, settingsController, backpackController));

            mvcManager.RegisterController(new PersistentExplorePanelOpenerController(
                PersistentExplorePanelOpenerController.CreateLazily(
                    (await assetsProvisioner.ProvideMainAssetAsync(settings.PersistentExploreOpenerPrefab, ct: ct)).Value.GetComponent<PersistentExploreOpenerView>(), null), mvcManager)
            );

            mvcManager.ShowAsync(PersistentExplorePanelOpenerController.IssueCommand(new EmptyParameter())).Forget();
        }

        public void Dispose()
        {
            navmapController.Dispose();
            backpackController.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            backpackController.InjectToWorld(ref builder, arguments.PlayerEntity);

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
