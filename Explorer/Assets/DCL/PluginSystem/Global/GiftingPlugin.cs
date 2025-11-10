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
using DCL.Backpack;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Factory;
using DCL.Backpack.Gifting.Presenters;
using DCL.Backpack.Gifting.Presenters.GiftTransfer.Commands;
using DCL.Backpack.Gifting.Services;
using DCL.Backpack.Gifting.Styling;
using DCL.Browser;
using DCL.Input;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
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
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IProfileRepository profileRepository;
        private readonly IInputBlock inputBlock;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IEquippedWearables equippedWearables;
        private readonly IEmoteProvider emoteProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly IEventBus eventBus;
        private readonly IWebBrowser webBrowser;
        private GiftSelectionController? giftSelectionController;
        private GiftTransferController? giftTransferStatusController;
        private GiftTransferSuccessController? giftTransferSuccessController;

        public GiftingPlugin(IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IProfileRepository profileRepository,
            IInputBlock inputBlock,
            IWearablesProvider wearablesProvider,
            IEquippedWearables equippedWearables,
            IEmoteProvider emoteProvider,
            IWeb3IdentityCache web3IdentityCache,
            IThumbnailProvider thumbnailProvider,
            IEventBus eventBus,
            IWebBrowser webBrowser)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.profileRepository = profileRepository;
            this.inputBlock = inputBlock;
            this.wearablesProvider = wearablesProvider;
            this.equippedWearables = equippedWearables;
            this.emoteProvider = emoteProvider;
            this.web3IdentityCache = web3IdentityCache;
            this.thumbnailProvider = thumbnailProvider;
            this.eventBus =  eventBus;
            this.webBrowser = webBrowser;
        }

        public void Dispose()
        {
            giftSelectionController?.Dispose();
        }

        public async UniTask InitializeAsync(GiftingSettings settings, CancellationToken ct)
        {
            var giftSelectionPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.GiftSelectionPopupPrefab, ct))
                .Value.GetComponent<GiftingView>();

            var giftTransferPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.GiftTransferPopupPrefab, ct))
                .Value.GetComponent<GiftTransferStatusView>();

            var giftTransferSuccessPopupPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.GiftTransferPopupSuccessPrefab, ct))
                .Value.GetComponent<GiftTransferSuccessView>();

            var (rarityColors, categoryIcons, rarityBackgrounds, rarityInfoPanelBackgroundsMapping) = await UniTask
                .WhenAll(assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.RarityColorMappings, ct),
                    assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.CategoryIconsMapping, ct),
                    assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.RarityBackgroundsMapping, ct),
                    assetsProvisioner.ProvideMainAssetValueAsync(settings.BackpackSettings.RarityInfoPanelBackgroundsMapping, ct));

            var giftTransferService = new MockGiftTransferService();

            var wearableCatalog = new WearableStylingCatalog(rarityColors,
                rarityBackgrounds,
                categoryIcons);

            var sendGiftCommand = new SendGiftCommand(giftTransferService, mvcManager);
            var giftTransferRequestCommand = new GiftTransferRequestCommand(eventBus, giftTransferService);
            var loadThumbnailCommand = new LoadGiftableItemThumbnailCommand(thumbnailProvider, eventBus);
            var giftTransferProgressCommand = new GiftTransferProgressCommand();
            var giftTransferResponseCommand = new GiftTransferResponseCommand();
            var giftTransferSignCommand = new GiftTransferSignCommand();

            var gridFactory = new GiftingGridPresenterFactory(eventBus,
                wearablesProvider,
                emoteProvider,
                web3IdentityCache,
                settings.EmbeddedEmotesAsURN(),
                loadThumbnailCommand,
                wearableCatalog,
                equippedWearables);

            giftSelectionController = new GiftSelectionController(
                GiftSelectionController.CreateLazily(giftSelectionPopupPrefab, null),
                profileRepositoryWrapper,
                profileRepository,
                inputBlock,
                gridFactory,
                mvcManager
            );
            
            giftTransferStatusController = new GiftTransferController(
                GiftTransferController.CreateLazily(giftTransferPopupPrefab, null),
                webBrowser,
                eventBus,
                mvcManager,
                giftTransferProgressCommand,
                giftTransferRequestCommand,
                giftTransferResponseCommand,
                giftTransferSignCommand
            );

            giftTransferSuccessController = new GiftTransferSuccessController(GiftTransferSuccessController.CreateLazily(giftTransferSuccessPopupPrefab,
                null));

            mvcManager.RegisterController(giftSelectionController);
            mvcManager.RegisterController(giftTransferStatusController);
            mvcManager.RegisterController(giftTransferSuccessController);
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