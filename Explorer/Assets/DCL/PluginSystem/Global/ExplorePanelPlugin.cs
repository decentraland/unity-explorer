using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.ExplorePanel;
using DCL.Navmap;
using DCL.ParcelsService;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Settings;
using DCL.UI;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using Global.Dynamic;
using MVC;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;

// ReSharper disable UnusedAutoPropertyAccessor.Local
namespace DCL.PluginSystem.Global
{
    public class ExplorePanelPlugin : DCLGlobalPluginBase<ExplorePanelPlugin.ExplorePanelSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly MapRendererContainer mapRendererContainer;
        private readonly IMVCManager mvcManager;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IProfileRepository profileRepository;
        private readonly ITeleportController teleportController;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWearableCatalog wearableCatalog;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly IWebBrowser webBrowser;
        private readonly IRealmNavigator realmNavigator;
        private readonly IEmoteCache emoteCache;
        private readonly DCLInput dclInput;
        private readonly IWebRequestController webRequestController;
        private bool isUIHidden;

        private NavmapController? navmapController;
        private BackpackSubPlugin backpackSubPlugin;
        private PersistentExploreOpenerView? exploreOpener;
        private PersistentExplorePanelOpenerController explorePanelOpener;

        public ExplorePanelPlugin(IAssetsProvisioner assetsProvisioner,
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
            IWebBrowser webBrowser,
            IEmoteCache emoteCache,
            IRealmNavigator realmNavigator,
            DCLInput dclInput)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mapRendererContainer = mapRendererContainer;
            this.placesAPIService = placesAPIService;
            this.teleportController = teleportController;
            this.webRequestController = webRequestController;
            this.web3IdentityCache = web3IdentityCache;
            this.wearableCatalog = wearableCatalog;
            this.characterPreviewFactory = characterPreviewFactory;
            this.profileRepository = profileRepository;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.webBrowser = webBrowser;
            this.realmNavigator = realmNavigator;
            this.emoteCache = emoteCache;
            this.dclInput = dclInput;
        }

        public override void Dispose()
        {
            navmapController?.Dispose();
            backpackSubPlugin.Dispose();
        }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(ExplorePanelSettings settings, CancellationToken ct)
        {
            backpackSubPlugin = new BackpackSubPlugin(assetsProvisioner, web3IdentityCache, characterPreviewFactory, wearableCatalog, profileRepository, emoteCache, settings.EmbeddedEmotes.Select(s => new URN(s)).ToArray());

            ExplorePanelView panelViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.ExplorePanelPrefab, ct: ct)).GetComponent<ExplorePanelView>();
            ControllerBase<ExplorePanelView, ExplorePanelParameter>.ViewFactoryMethod viewFactoryMethod = ExplorePanelController.Preallocate(panelViewAsset, null, out ExplorePanelView explorePanelView);

            navmapController = new NavmapController(navmapView: explorePanelView.GetComponentInChildren<NavmapView>(), mapRendererContainer.MapRenderer, placesAPIService, webRequestController, realmNavigator);
            await navmapController.InitialiseAssetsAsync(assetsProvisioner, ct);

            var settingsController = new SettingsController(explorePanelView.GetComponentInChildren<SettingsView>());
            exploreOpener = (await assetsProvisioner.ProvideMainAssetAsync(settings.PersistentExploreOpenerPrefab, ct: ct)).Value.GetComponent<PersistentExploreOpenerView>();

            ContinueInitialization? backpackInitialization = await backpackSubPlugin.InitializeAsync(settings.BackpackSettings, explorePanelView.GetComponentInChildren<BackpackView>(), ct);

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                backpackInitialization.Invoke(ref builder, arguments);

                mvcManager.RegisterController(new ExplorePanelController(viewFactoryMethod, navmapController, settingsController, backpackSubPlugin.backpackController!, arguments.PlayerEntity, builder.World,
                    new ProfileWidgetController(() => explorePanelView.ProfileWidget, web3IdentityCache, profileRepository, webRequestController),
                    new SystemMenuController(() => explorePanelView.SystemMenu, builder.World, arguments.PlayerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow),
                    dclInput));

                explorePanelOpener = new PersistentExplorePanelOpenerController(
                    PersistentExplorePanelOpenerController.CreateLazily(exploreOpener, null), mvcManager);

                mvcManager.RegisterController(explorePanelOpener
                );

                RegisterHotkeys();
            };
        }

        private void RegisterHotkeys()
        {
            dclInput.Shortcuts.MainMenu.performed += OnMainMenuHotkeyPressed;
            dclInput.Shortcuts.Map.performed += OnMapHotkeyPressed;
            dclInput.Shortcuts.Settings.performed += OnSettingsHotkeyPressed;
            dclInput.Shortcuts.Backpack.performed += OnBackpackHotkeyPressed;
        }

        private void OnMainMenuHotkeyPressed(InputAction.CallbackContext obj)
        {
            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExplorePanelController.GetLastShownSection())));
        }

        private void OnMapHotkeyPressed(InputAction.CallbackContext obj)
        {
            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Navmap)));
        }

        private void OnSettingsHotkeyPressed(InputAction.CallbackContext obj)
        {
            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Settings)));
        }

        private void OnBackpackHotkeyPressed(InputAction.CallbackContext obj)
        {
            mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack)));
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

            [field: SerializeField]
            public string[] EmbeddedEmotes { get; private set; }
        }
    }
}
