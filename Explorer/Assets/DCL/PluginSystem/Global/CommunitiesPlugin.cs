using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.Chat;
using DCL.InWorldCamera;
using DCL.Input;
using DCL.Clipboard;
using DCL.Communities;
using DCL.Communities.CommunitiesCard;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Communities.CommunityCreation;
using DCL.EventsApi;
using DCL.ExplorePanel;
using DCL.Friends;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SocialService;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.VoiceChat;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;

namespace DCL.PluginSystem.Global
{
    public class CommunitiesPlugin : IDCLGlobalPlugin<CommunitiesPluginSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IInputBlock inputBlock;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly IPlacesAPIService placesAPIService;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmNavigator realmNavigator;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;
        private readonly HttpEventsApiService eventsApiService;
        private readonly ChatEventBus chatEventBus;
        private readonly RPCCommunitiesService rpcCommunitiesService;
        private readonly NotificationHandler notificationHandler;
        private readonly IProfileRepository profileRepository;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly GalleryEventBus galleryEventBus;
        private readonly IAnalyticsController analytics;
        private readonly HomePlaceEventBus homePlaceEventBus;
        private readonly DCLInput dclInput;

        private CommunityCardController? communityCardController;
        private CommunityCreationEditionController? communityCreationEditionController;

        public CommunitiesPlugin(IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IInputBlock inputBlock,
            ICameraReelStorageService cameraReelStorageService,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            ProfileRepositoryWrapper profileDataProvider,
            ObjectProxy<IFriendsService> friendServiceProxy,
            CommunitiesDataProvider communitiesDataProvider,
            IWebRequestController webRequestController,
            IPlacesAPIService placesAPIService,
            ISelfProfile selfProfile,
            IRealmNavigator realmNavigator,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser,
            HttpEventsApiService eventsApiService,
            ChatEventBus chatEventBus,
            GalleryEventBus galleryEventBus,
            CommunitiesEventBus communitiesEventBus,
            IRPCSocialServices rpcSocialServices,
            IProfileRepository profileRepository,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWeb3IdentityCache web3IdentityCache,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            IAnalyticsController analytics,
            HomePlaceEventBus homePlaceEventBus)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.inputBlock = inputBlock;
            this.cameraReelStorageService = cameraReelStorageService;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorage;
            this.profileRepositoryWrapper = profileDataProvider;
            this.friendServiceProxy = friendServiceProxy;
            this.communitiesDataProvider = communitiesDataProvider;
            this.webRequestController = webRequestController;
            this.placesAPIService = placesAPIService;
            this.selfProfile = selfProfile;
            this.realmNavigator = realmNavigator;
            this.clipboard = clipboard;
            this.webBrowser = webBrowser;
            this.eventsApiService = eventsApiService;
            this.chatEventBus = chatEventBus;
            this.profileRepository = profileRepository;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.web3IdentityCache = web3IdentityCache;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.galleryEventBus = galleryEventBus;
            this.analytics = analytics;
            this.homePlaceEventBus = homePlaceEventBus;
            dclInput = DCLInput.Instance;
            rpcCommunitiesService = new RPCCommunitiesService(rpcSocialServices, communitiesEventBus);
            notificationHandler = new NotificationHandler(realmNavigator);

            dclInput.Shortcuts.Places.performed += OnInputShortcutsPlacesPerformedAsync;
            dclInput.Shortcuts.Events.performed += OnInputShortcutsEventsPerformedAsync;
        }

        public void Dispose()
        {
            communityCardController?.Dispose();
            communityCreationEditionController?.Dispose();
            notificationHandler.Dispose();
            rpcCommunitiesService.Dispose();
            dclInput.Shortcuts.Places.performed -= OnInputShortcutsPlacesPerformedAsync;
            dclInput.Shortcuts.Events.performed -= OnInputShortcutsEventsPerformedAsync;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(CommunitiesPluginSettings settings, CancellationToken ct)
        {
            CommunityCardView communityCardViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CommunityCardPrefab, ct: ct)).GetComponent<CommunityCardView>();
            ControllerBase<CommunityCardView, CommunityCardParameter>.ViewFactoryMethod viewFactoryMethod = CommunityCardController.Preallocate(communityCardViewAsset, null, out CommunityCardView communityCardView);

            communityCardController = new CommunityCardController(
                viewFactoryMethod,
                mvcManager,
                cameraReelStorageService,
                cameraReelScreenshotsStorage,
                friendServiceProxy,
                communitiesDataProvider,
                webRequestController,
                profileRepositoryWrapper,
                placesAPIService,
                realmNavigator,
                clipboard,
                webBrowser,
                eventsApiService,
                chatEventBus,
                decentralandUrlsSource,
                web3IdentityCache,
                profileRepository,
                galleryEventBus,
                voiceChatOrchestrator,
                inputBlock,
                selfProfile,
                analytics,
                homePlaceEventBus);

            mvcManager.RegisterController(communityCardController);

            VoiceChatCommunityCardBridge.SetOpenCommunityCardAction(communityId =>
                mvcManager.ShowAndForget(CommunityCardController.IssueCommand(new CommunityCardParameter(communityId)), ct: ct));

            CommunityCreationEditionView communityCreationEditionViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.CommunityCreationEditionPrefab, ct: ct)).GetComponent<CommunityCreationEditionView>();
            ControllerBase<CommunityCreationEditionView, CommunityCreationEditionParameter>.ViewFactoryMethod communityCreationEditionViewFactoryMethod = CommunityCreationEditionController.Preallocate(communityCreationEditionViewAsset, null, out CommunityCreationEditionView communityCreationEditionView);
            communityCreationEditionController = new CommunityCreationEditionController(
                communityCreationEditionViewFactoryMethod,
                webBrowser,
                inputBlock,
                communitiesDataProvider,
                placesAPIService,
                selfProfile,
                mvcManager,
                profileRepository);
            mvcManager.RegisterController(communityCreationEditionController);

            rpcCommunitiesService.TrySubscribeToConnectivityStatusAsync(ct).Forget();
        }

        private void OnInputShortcutsEventsPerformedAsync(InputAction.CallbackContext _) =>
            mvcManager.ShowAndForget(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Events)));


        private void OnInputShortcutsPlacesPerformedAsync(InputAction.CallbackContext _) =>
            mvcManager.ShowAndForget(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Places)));
    }

    [Serializable]
    public class CommunitiesPluginSettings : IDCLPluginSettings
    {
        [field: Header("Community Card")]
        [field: SerializeField] internal AssetReferenceGameObject CommunityCardPrefab { get; private set; }

        [field: Header("Community Creation Edition Wizard")]
        [field: SerializeField] internal AssetReferenceGameObject CommunityCreationEditionPrefab { get; private set; }
    }
}
