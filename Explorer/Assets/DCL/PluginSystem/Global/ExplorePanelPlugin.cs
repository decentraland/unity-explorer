using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.ExplorePanel;
using DCL.Navmap;
using DCL.ParcelsService;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Settings;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Global.Dynamic;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace DCL.PluginSystem.Global
{
    public class ExplorePanelPlugin : DCLGlobalPluginBase<ExplorePanelPlugin.ExplorePanelSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        private readonly BackpackSubPlugin backpackSubPlugin;
        private readonly MapRendererContainer mapRendererContainer;
        private readonly IMVCManager mvcManager;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IProfileRepository profileRepository;
        private readonly ITeleportController teleportController;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWebBrowser webBrowser;
        private readonly IWebRequestController webRequestController;

        private NavmapController? navmapController;

        public ExplorePanelPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            MapRendererContainer mapRendererContainer,
            IPlacesAPIService placesAPIService,
            ITeleportController teleportController,
            IWebRequestController webRequestController,
            IWeb3IdentityCache web3IdentityCache,
            IWearableCatalog wearableCatalog,
            ICharacterPreviewFactory characterPreviewFactory,
            IProfileRepository profileRepository,
            IWeb3Authenticator web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            ISelfProfile selfProfile,
            IEquippedWearables equippedWearables,
            IWebBrowser webBrowser)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mapRendererContainer = mapRendererContainer;
            this.placesAPIService = placesAPIService;
            this.teleportController = teleportController;
            this.webRequestController = webRequestController;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.webBrowser = webBrowser;

            backpackSubPlugin = new BackpackSubPlugin(assetsProvisioner, web3IdentityCache, characterPreviewFactory, wearableCatalog, selfProfile, equippedWearables, profileRepository);
        }

        public override void Dispose()
        {
            navmapController?.Dispose();
            backpackSubPlugin.Dispose();
        }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(ExplorePanelSettings settings, CancellationToken ct)
        {
            ExplorePanelView panelViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.ExplorePanelPrefab, ct: ct)).GetComponent<ExplorePanelView>();
            ControllerBase<ExplorePanelView, ExplorePanelParameter>.ViewFactoryMethod viewFactoryMethod = ExplorePanelController.Preallocate(panelViewAsset, null, out ExplorePanelView explorePanelView);

            navmapController = new NavmapController(navmapView: explorePanelView.GetComponentInChildren<NavmapView>(), mapRendererContainer.MapRenderer, placesAPIService, teleportController, webRequestController, mvcManager);
            await navmapController.InitialiseAssetsAsync(assetsProvisioner, ct);

            var settingsController = new SettingsController(explorePanelView.GetComponentInChildren<SettingsView>());
            PersistentExploreOpenerView? exploreOpener = (await assetsProvisioner.ProvideMainAssetAsync(settings.PersistentExploreOpenerPrefab, ct: ct)).Value.GetComponent<PersistentExploreOpenerView>();

            ContinueInitialization? backpackInitialization = await backpackSubPlugin.InitializeAsync(settings.BackpackSettings, explorePanelView.GetComponentInChildren<BackpackView>(), ct);

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                backpackInitialization.Invoke(ref builder, arguments);

                mvcManager.RegisterController(new ExplorePanelController(viewFactoryMethod, navmapController, settingsController, backpackSubPlugin.backpackController!,
                    new ProfileWidgetController(() => explorePanelView.ProfileWidget, web3IdentityCache, profileRepository, webRequestController),
                    new SystemMenuController(() => explorePanelView.SystemMenu, builder.World, arguments.PlayerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow)));

                mvcManager.RegisterController(new PersistentExplorePanelOpenerController(
                    PersistentExplorePanelOpenerController.CreateLazily(exploreOpener, null), mvcManager)
                );
            };
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

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

            [field: SerializeField]
            public BackpackSettings BackpackSettings { get; private set; }
        }
    }
}
