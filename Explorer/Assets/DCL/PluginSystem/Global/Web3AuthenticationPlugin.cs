using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.Wearables;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Clipboard;
using DCL.DebugUtilities;
using DCL.EventsApi;
using DCL.Friends;
using DCL.Input;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Places;
using DCL.PlacesAPIService;
using DCL.PrivateWorlds;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class Web3AuthenticationPlugin : IDCLGlobalPlugin<Web3AuthPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ICompositeWeb3Provider web3Authenticator;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly IWebBrowser webBrowser;
        private readonly IRealmData realmData;
        private readonly IWeb3IdentityCache storedIdentityProvider;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly SplashScreen splashScreen;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly Arch.Core.World world;
        private readonly AudioMixerVolumesController audioMixerVolumesController;
        private readonly IInputBlock inputBlock;
        private readonly AudioClipConfig backgroundMusic;
        private readonly IAppArgs appArgs;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmNavigator realmNavigator;
        private readonly IGlobalRealmController realmController;
        private readonly StartParcel startParcel;
        private readonly ISystemClipboard clipboard;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly HomePlaceEventBus homePlaceEventBus;
        private readonly IWorldPermissionsService worldPermissionsService;
        private readonly HttpEventsApiService eventsApiService;
        private readonly ICursor cursor;

        private CancellationTokenSource? cancellationTokenSource;
        private AuthenticationScreenController authenticationScreenController = null!;
        private Web3ConfirmationPopupView? transactionConfirmationView;

        public Web3AuthenticationPlugin(
            IAssetsProvisioner assetsProvisioner,
            ICompositeWeb3Provider web3Authenticator,
            IDebugContainerBuilder debugContainerBuilder,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IRealmData realmData,
            IWeb3IdentityCache storedIdentityProvider,
            ICharacterPreviewFactory characterPreviewFactory,
            SplashScreen splashScreen,
            AudioMixerVolumesController audioMixerVolumesController,
            IInputBlock inputBlock,
            CharacterPreviewEventBus characterPreviewEventBus,
            AudioClipConfig backgroundMusic,
            Arch.Core.World world,
            IAppArgs appArgs,
            IWearablesProvider wearablesProvider,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            ProfileChangesBus profileChangesBus,
            IPlacesAPIService placesAPIService,
            IRealmNavigator realmNavigator,
            IGlobalRealmController realmController,
            StartParcel startParcel,
            ISystemClipboard clipboard,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            HomePlaceEventBus homePlaceEventBus,
            IWorldPermissionsService worldPermissionsService,
            HttpEventsApiService eventsApiService,
            ICursor cursor
        )
        {
            this.assetsProvisioner = assetsProvisioner;
            this.web3Authenticator = web3Authenticator;
            this.debugContainerBuilder = debugContainerBuilder;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.webBrowser = webBrowser;
            this.realmData = realmData;
            this.storedIdentityProvider = storedIdentityProvider;
            this.characterPreviewFactory = characterPreviewFactory;
            this.splashScreen = splashScreen;
            this.audioMixerVolumesController = audioMixerVolumesController;
            this.inputBlock = inputBlock;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.backgroundMusic = backgroundMusic;
            this.world = world;
            this.appArgs = appArgs;
            this.wearablesProvider = wearablesProvider;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.profileChangesBus = profileChangesBus;
            this.placesAPIService = placesAPIService;
            this.realmNavigator = realmNavigator;
            this.realmController = realmController;
            this.startParcel = startParcel;
            this.clipboard = clipboard;
            this.friendServiceProxy = friendServiceProxy;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.homePlaceEventBus = homePlaceEventBus;
            this.worldPermissionsService = worldPermissionsService;
            this.eventsApiService = eventsApiService;
            this.cursor = cursor;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(Web3AuthPluginSettings settings, CancellationToken ct)
        {
            AuthenticationScreenView authScreenPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.AuthScreenPrefab, ct: ct)).Value;
            ControllerBase<AuthenticationScreenView, ControllerNoData>.ViewFactoryMethod authScreenFactory = AuthenticationScreenController.CreateLazily(authScreenPrefab, null);

            PlaceCategoriesSO placeCategories = (await assetsProvisioner.ProvideMainAssetAsync(settings.PlaceCategoriesSO, ct)).Value;

            var placesDeps = new AuthPlacesDependencies(
                placesAPIService,
                realmNavigator,
                realmController,
                startParcel,
                clipboard,
                friendServiceProxy,
                profileRepositoryWrapper,
                homePlaceEventBus,
                worldPermissionsService,
                eventsApiService,
                cursor,
                placeCategories,
                mvcManager);

            authenticationScreenController = new AuthenticationScreenController(authScreenFactory,
                web3Authenticator,
                selfProfile,
                webBrowser,
                storedIdentityProvider,
                characterPreviewFactory,
                splashScreen,
                characterPreviewEventBus,
                audioMixerVolumesController,
                settings.BuildData.InstallSource,
                world,
                settings.EmotesSettings,
                inputBlock,
                backgroundMusic,
                wearablesProvider,
                webRequestController,
                decentralandUrlsSource,
                profileChangesBus,
                placesDeps);

            mvcManager.RegisterController(authenticationScreenController);

            Web3ConfirmationPopupView txConfPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.TransactionConfirmationPopupPrefab, ct: ct)).Value;
            InitializeTransactionConfirmationPopup(txConfPopupPrefab);
        }

        private void InitializeTransactionConfirmationPopup(Web3ConfirmationPopupView popupPrefab)
        {
            transactionConfirmationView = Object.Instantiate(popupPrefab);
            transactionConfirmationView.SetDrawOrder(new CanvasOrdering(CanvasOrdering.SortingLayer.POPUP, 500));
            transactionConfirmationView.gameObject.SetActive(false);
            web3Authenticator.SetTransactionConfirmationCallback(transactionConfirmationView.ShowAsync);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LoginFromDebugPanelSystem.InjectToWorld(ref builder, debugContainerBuilder, web3Authenticator, mvcManager, realmData);
        }
    }

    [Serializable]
    public struct Web3AuthPluginSettings : IDCLPluginSettings
    {
        [field: Header(nameof(Web3AuthenticationPlugin) + "." + nameof(Web3AuthPluginSettings))]
        [field: Space]
        [field: SerializeField] public AuthScreenObjectRef AuthScreenPrefab { get; private set; }
        [field: SerializeField] public TransactionConfirmationPopupRef TransactionConfirmationPopupPrefab { get; private set; }
        [field: SerializeField] public BuildData BuildData { get; private set; }

        [field: Tooltip("Should point to the same PlaceCategoriesSO asset used by the Explore panel")]
        [field: SerializeField] public AssetReferenceT<PlaceCategoriesSO> PlaceCategoriesSO { get; private set; }

        [field: Space]
        [field: SerializeField] public AuthScreenEmotesSettings EmotesSettings { get; private set; }

        [Serializable]
        public class AuthScreenObjectRef : ComponentReference<AuthenticationScreenView>
        {
            public AuthScreenObjectRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class TransactionConfirmationPopupRef : ComponentReference<Web3ConfirmationPopupView>
        {
            public TransactionConfirmationPopupRef(string guid) : base(guid) { }
        }
    }
}
