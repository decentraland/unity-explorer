using System.Collections.Generic;
using System.Linq;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack.Gifting.Views;
using MVC;
using System.Threading;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Factory;
using DCL.Backpack.Gifting.Notifications;
using DCL.Backpack.Gifting.Presenters;
using DCL.Backpack.Gifting.Presenters.GiftTransfer.Commands;
using DCL.Backpack.Gifting.Services;
using DCL.Backpack.Gifting.Services.GiftingInventory;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Services.SnapshotEquipped;
using DCL.Backpack.Gifting.Styling;
using DCL.Browser;
using DCL.Input;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class GiftingPlugin : IDCLGlobalPlugin<GiftingPlugin.GiftingSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly IPendingTransferService pendingTransferService;
        private readonly IAvatarEquippedStatusProvider equippedStatusProvider;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IProfileRepository profileRepository;
        private readonly IInputBlock inputBlock;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IWearableStorage wearableStorage;
        private readonly IEmoteStorage emoteStorage;
        private readonly IEquippedWearables equippedWearables;
        private readonly IEmoteProvider emoteProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly IEventBus eventBus;
        private readonly IWebBrowser webBrowser;
        private readonly IEthereumApi ethereumApi;
        private readonly ISelfProfile selfProfile;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWeb3VerifiedAuthenticator dappWeb3Authenticator;
        private readonly ISharedSpaceManager sharedSpaceManager;
        
        private GiftSelectionController? giftSelectionController;
        private GiftTransferController? giftTransferStatusController;
        private GiftTransferSuccessController? giftTransferSuccessController;
        private GiftReceivedPopupController giftReceivedPopupController;
        private GiftNotificationOpenerController giftNotificationOpenerController;

        public GiftingPlugin(IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IPendingTransferService pendingTransferService,
            IAvatarEquippedStatusProvider equippedStatusProvider,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IProfileRepository profileRepository,
            IInputBlock inputBlock,
            IWearablesProvider wearablesProvider,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage,
            IEquippedWearables equippedWearables,
            IEmoteProvider emoteProvider,
            IWeb3IdentityCache web3IdentityCache,
            IThumbnailProvider thumbnailProvider,
            IEventBus eventBus,
            IWebBrowser webBrowser,
            IEthereumApi ethereumApi,
            ISelfProfile selfProfile,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWeb3VerifiedAuthenticator dappWeb3Authenticator,
            ISharedSpaceManager sharedSpaceManager)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.pendingTransferService = pendingTransferService;
            this.equippedStatusProvider = equippedStatusProvider;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.profileRepository = profileRepository;
            this.inputBlock = inputBlock;
            this.wearablesProvider = wearablesProvider;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
            this.equippedWearables = equippedWearables;
            this.emoteProvider = emoteProvider;
            this.web3IdentityCache = web3IdentityCache;
            this.thumbnailProvider = thumbnailProvider;
            this.eventBus =  eventBus;
            this.webBrowser = webBrowser;
            this.ethereumApi = ethereumApi;
            this.selfProfile = selfProfile;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.dappWeb3Authenticator = dappWeb3Authenticator;
            this.sharedSpaceManager = sharedSpaceManager;
        }

        public void Dispose()
        {
            giftSelectionController?.Dispose();
            giftReceivedPopupController?.Dispose();
        }

        public async UniTask InitializeAsync(GiftingSettings settings, CancellationToken ct)
        {
            var giftSelectionPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.GiftSelectionPopupPrefab, ct))
                .Value.GetComponent<GiftingView>();

            var giftTransferPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.GiftTransferPopupPrefab, ct))
                .Value.GetComponent<GiftTransferStatusView>();

            var giftTransferSuccessPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.GiftTransferPopupSuccessPrefab, ct))
                .Value.GetComponent<GiftTransferSuccessView>();

            var giftReceivedView = (await assetsProvisioner.ProvideMainAssetAsync(settings.GiftReceivedPopupPrefab, ct))
                .Value.GetComponent<GiftReceivedPopupView>();

            var (rarityColors, categoryIcons, rarityBackgrounds, rarityInfoPanelBackgroundsMapping) = await UniTask
                .WhenAll(assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.RarityColorMappings, ct),
                    assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.CategoryIconsMapping, ct),
                    assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.RarityBackgroundsMapping, ct),
                    assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.RarityInfoPanelBackgroundsMapping, ct));
            
            var giftTransferService = new Web3GiftTransferService(ethereumApi);
            
            var giftInventoryService = new GiftInventoryService(wearableStorage,
                emoteStorage,
                equippedStatusProvider,
                pendingTransferService);

            var wearableCatalog = new WearableStylingCatalog(rarityColors,
                rarityBackgrounds,
                categoryIcons);

            var giftTransferRequestCommand = new GiftTransferRequestCommand(eventBus,
                web3IdentityCache,
                giftTransferService,
                pendingTransferService);

            var loadThumbnailCommand = new LoadGiftableItemThumbnailCommand(thumbnailProvider,
                eventBus);

            giftReceivedPopupController = new GiftReceivedPopupController(
                GiftReceivedPopupController.CreateLazily(giftReceivedView, null),
                profileRepository,
                wearableCatalog,
                wearableStorage,
                emoteStorage,
                thumbnailProvider,
                sharedSpaceManager
            );

            giftNotificationOpenerController = new GiftNotificationOpenerController(mvcManager);
            
            var gridFactory = new GiftingGridPresenterFactory(eventBus,
                wearablesProvider,
                emoteProvider,
                web3IdentityCache,
                loadThumbnailCommand,
                wearableCatalog,
                pendingTransferService,
                equippedStatusProvider,
                wearableStorage,
                emoteStorage);

            var componentFactory = new GiftSelectionComponentFactory(profileRepository,
                profileRepositoryWrapper,
                inputBlock,
                gridFactory);

            giftSelectionController = new GiftSelectionController(
                GiftSelectionController
                    .CreateLazily(giftSelectionPopupPrefab, null),
                componentFactory,
                giftInventoryService,
                equippedStatusProvider,
                profileRepository,
                mvcManager
            );

            giftTransferStatusController = new GiftTransferController(
                GiftTransferController
                    .CreateLazily(giftTransferPopupPrefab, null),
                webBrowser,
                eventBus,
                mvcManager,
                decentralandUrlsSource,
                giftTransferRequestCommand,
                dappWeb3Authenticator,
                dappWeb3Authenticator.CancelCurrentWeb3Operation
            );

            giftTransferSuccessController = new GiftTransferSuccessController(GiftTransferSuccessController
                .CreateLazily(giftTransferSuccessPopupPrefab,
                    null));

            mvcManager.RegisterController(giftSelectionController);
            mvcManager.RegisterController(giftTransferStatusController);
            mvcManager.RegisterController(giftTransferSuccessController);
            mvcManager.RegisterController(giftReceivedPopupController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public class GiftingSettings : IDCLPluginSettings
        {
            [field: Header(nameof(GiftingPlugin) + "." + nameof(GiftingSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject GiftSelectionPopupPrefab;

            [field: SerializeField]
            public AssetReferenceGameObject GiftTransferPopupPrefab;

            [field: SerializeField]
            public AssetReferenceGameObject GiftTransferPopupSuccessPrefab;

            [field: Header("Notifications")]
            [field: SerializeField]
            public AssetReferenceGameObject GiftReceivedPopupPrefab;

            [Header("Localization / Constants")]
            [SerializeField] public string GiftTransferPopupStatusTitle = "Preparing Gift for";

            [SerializeField] public string GiftTransferPopupStatusWaitingMessage = "A browser window should open for you to confirm the transaction.";
            [SerializeField] public string GiftTransferPopupStatusProcessingMessage = "Processing...";

            [SerializeField] public string GiftTransferErrorPopupTitle = "Something went wrong";
            [SerializeField] public string GiftTransferErrorPopupAdditionalUrlTitle = "Your gift wasn't delivered. Please try again of contact Support.";
            [SerializeField] public string GiftTransferErrorPopupCloseButtonText = "CLOSE";
            [SerializeField] public string GiftTransferErrorPopupConfirmButtonText = "TRY AGAIN";
            
            [field: SerializeField]
            public BackpackSettings BackpackSettings { get; private set; }

            [field: SerializeField]
            public string[] EmbeddedEmotes { get; private set; }

            public IReadOnlyCollection<URN> EmbeddedEmotesAsURN()
            {
                return EmbeddedEmotes.Select(s => new URN(s)).ToArray();
            }
        }
    }
}