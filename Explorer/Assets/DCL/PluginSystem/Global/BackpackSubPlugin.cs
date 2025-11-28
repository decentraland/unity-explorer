using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.ThirdParty;
using DCL.Backpack;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.CharacterPreview;
using DCL.Backpack.EmotesSection;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using Global.AppArgs;
using MVC;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.FeatureFlags;
using UnityEngine.Pool;
using Utility;

namespace DCL.PluginSystem.Global
{
    internal class BackpackSubPlugin : IDisposable
    {
        private readonly FeatureFlagsConfiguration featureFlags;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWearableStorage wearableStorage;
        private readonly ISelfProfile selfProfile;
        private readonly IProfileCache profileCache;
        private readonly IEquippedWearables equippedWearables;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IEmoteStorage emoteStorage;
        private readonly IReadOnlyCollection<URN> embeddedEmotes;
        private readonly IWeb3IdentityCache web3Identity;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IThirdPartyNftProviderSource thirdPartyNftProviderSource;
        private readonly IWearablesProvider wearablesProvider;
        private readonly ICursor cursor;
        private readonly IEmoteProvider emoteProvider;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly IInputBlock inputBlock;
        private readonly IAppArgs appArgs;
        private readonly IWebBrowser webBrowser;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly OutfitsRepository outfitsRepository;
        private readonly IRealmData realmData;
        private readonly IWebRequestController webController;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly IEventBus eventBus;
        private readonly SmartWearableCache smartWearableCache;
        private readonly IMVCManager mvcManager;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private BackpackBusController? busController;
        private BackpackEquipStatusController? backpackEquipStatusController;

        internal BackpackController? backpackController { get; private set; }

        public BackpackSubPlugin(
            FeatureFlagsConfiguration featureFlags,
            IAssetsProvisioner assetsProvisioner,
            IWeb3IdentityCache web3Identity,
            ICharacterPreviewFactory characterPreviewFactory,
            IWearableStorage wearableStorage,
            ISelfProfile selfProfile,
            IProfileCache profileCache,
            IEquippedWearables equippedWearables,
            IEquippedEmotes equippedEmotes,
            IEmoteStorage emoteStorage,
            IReadOnlyCollection<URN> embeddedEmotes,
            CharacterPreviewEventBus characterPreviewEventBus,
            IBackpackEventBus backpackEventBus,
            IThirdPartyNftProviderSource thirdPartyNftProviderSource,
            IWearablesProvider wearablesProvider,
            IInputBlock inputBlock,
            ICursor cursor,
            IEmoteProvider emoteProvider,
            Arch.Core.World world,
            Entity playerEntity,
            IAppArgs appArgs,
            IWebBrowser webBrowser,
            WarningNotificationView inWorldWarningNotificationView,
            IThumbnailProvider thumbnailProvider,
            ProfileChangesBus profileChangesBus,
            OutfitsRepository outfitsRepository,
            IRealmData realmData,
            IWebRequestController webController,
            INftNamesProvider nftNamesProvider,
            IEventBus eventBus,
            SmartWearableCache smartWearableCache,
            IMVCManager mvcManager,
            IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.featureFlags = featureFlags;
            this.assetsProvisioner = assetsProvisioner;
            this.web3Identity = web3Identity;
            this.characterPreviewFactory = characterPreviewFactory;
            this.wearableStorage = wearableStorage;
            this.selfProfile = selfProfile;
            this.profileCache = profileCache;
            this.equippedWearables = equippedWearables;
            this.equippedEmotes = equippedEmotes;
            this.emoteStorage = emoteStorage;
            this.embeddedEmotes = embeddedEmotes;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.backpackEventBus = backpackEventBus;
            this.thirdPartyNftProviderSource = thirdPartyNftProviderSource;
            this.wearablesProvider = wearablesProvider;
            this.cursor = cursor;
            this.inputBlock = inputBlock;
            this.emoteProvider = emoteProvider;
            this.world = world;
            this.playerEntity = playerEntity;
            this.appArgs = appArgs;
            this.webBrowser = webBrowser;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
            this.thumbnailProvider = thumbnailProvider;
            this.profileChangesBus = profileChangesBus;
            this.outfitsRepository = outfitsRepository;
            this.realmData = realmData;
            this.webController = webController;
            this.nftNamesProvider = nftNamesProvider;
            this.eventBus = eventBus;
            this.smartWearableCache = smartWearableCache;
            this.mvcManager = mvcManager;
            this.decentralandUrlsSource = decentralandUrlsSource;

            backpackCommandBus = new BackpackCommandBus();
        }

        internal async UniTask InitializeAsync(
            BackpackSettings backpackSettings,
            BackpackView view,
            CancellationToken ct)
        {
            // Initialize assets that do not require World
            var sortController = new BackpackSortController(view.BackpackSortView);

            busController = new BackpackBusController(wearableStorage,
                backpackEventBus,
                backpackCommandBus,
                equippedWearables,
                equippedEmotes,
                emoteStorage);

            var deleteIcon = await assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.DeleteOutfitIcon, ct);

            (NFTColorsSO rarityColorMappings, NftTypeIconSO categoryIconsMapping, NftTypeIconSO rarityBackgroundsMapping, NftTypeIconSO rarityInfoPanelBackgroundsMapping) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityColorMappings, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.CategoryIconsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityBackgroundsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityInfoPanelBackgroundsMapping, ct));

            (ColorPresetsSO hairColors, ColorPresetsSO eyesColors, ColorPresetsSO bodyshapeColors) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.HairColors, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.EyesColors, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.BodyshapeColors, ct));

            PageButtonView pageButtonView = (await assetsProvisioner.ProvideMainAssetAsync(backpackSettings.PageButtonView, ct)).Value.GetComponent<PageButtonView>().EnsureNotNull();
            ColorToggleView colorToggle = (await assetsProvisioner.ProvideMainAssetAsync(backpackSettings.ColorToggle, ct)).Value.GetComponent<ColorToggleView>().EnsureNotNull();

            AvatarView avatarView = view.GetComponentInChildren<AvatarView>().EnsureNotNull();

            var wearableInfoPanelController = new BackpackInfoPanelController(
                avatarView.backpackInfoPanelView,
                backpackEventBus,
                categoryIconsMapping,
                rarityInfoPanelBackgroundsMapping,
                rarityColorMappings,
                equippedWearables,
                BackpackInfoPanelController.AttachmentType.Wearable,
                thirdPartyNftProviderSource
            );

            EmotesView emoteView = view.GetComponentInChildren<EmotesView>().EnsureNotNull();

            BackpackInfoPanelController emoteInfoPanelController = new BackpackInfoPanelController(
                emoteView.BackpackInfoPanelView,
                backpackEventBus,
                categoryIconsMapping,
                rarityInfoPanelBackgroundsMapping,
                rarityColorMappings,
                equippedWearables,
                BackpackInfoPanelController.AttachmentType.Emote,
                thirdPartyNftProviderSource
            );

            //not injected anywhere
            _ = new BackpackEmoteBreadCrumbController(emoteView.BreadCrumb, backpackEventBus);

            ObjectPool<BackpackEmoteGridItemView>? emoteGridPool = await BackpackEmoteGridController.InitializeAssetsAsync(assetsProvisioner, emoteView.GridView, ct);

            await wearableInfoPanelController.InitialiseAssetsAsync(assetsProvisioner, ct);

            ObjectPool<BackpackItemView>? gridPool = await BackpackGridController.InitialiseAssetsAsync(assetsProvisioner, avatarView.backpackGridView, ct);

            var gridController = new BackpackGridController(
                avatarView.backpackGridView,
                backpackCommandBus,
                backpackEventBus,
                rarityBackgroundsMapping,
                rarityColorMappings,
                categoryIconsMapping,
                equippedWearables,
                sortController,
                pageButtonView,
                gridPool,
                thumbnailProvider,
                wearablesProvider,
                webBrowser,
                colorToggle,
                hairColors,
                eyesColors,
                bodyshapeColors,
                wearableStorage,
                smartWearableCache,
                mvcManager
            );

            var emoteGridController = new BackpackEmoteGridController(emoteView.GridView, backpackCommandBus, backpackEventBus,
                web3Identity, rarityBackgroundsMapping, rarityColorMappings, categoryIconsMapping, equippedEmotes,
                sortController, pageButtonView, emoteGridPool, emoteProvider, embeddedEmotes, this.thumbnailProvider, webBrowser, appArgs, emoteStorage);

            var emotesController = new EmotesController(emoteView,
                new BackpackEmoteSlotsController(emoteView.Slots, backpackEventBus, backpackCommandBus, rarityBackgroundsMapping), emoteGridController);

            var backpackCharacterPreviewController = new BackpackCharacterPreviewController(view.CharacterPreviewView,
                characterPreviewFactory, backpackEventBus, world, equippedEmotes, characterPreviewEventBus);

            backpackEquipStatusController = new BackpackEquipStatusController(
                backpackEventBus,
                equippedEmotes,
                equippedWearables,
                selfProfile,
                profileCache,
                emoteStorage,
                wearableStorage,
                web3Identity,
                world,
                playerEntity,
                appArgs,
                inWorldWarningNotificationView,
                profileChangesBus,
                realmData
            );

            backpackController = new BackpackController(
                view,
                featureFlags,
                selfProfile,
                webBrowser,
                avatarView,
                rarityBackgroundsMapping,
                backpackCommandBus,
                backpackEventBus,
                gridController,
                wearableInfoPanelController,
                emoteInfoPanelController,
                world,
                playerEntity,
                emoteGridController,
                avatarView.GetComponentsInChildren<AvatarSlotView>().EnsureNotNull(),
                emotesController,
                backpackCharacterPreviewController,
                this.thumbnailProvider,
                inputBlock,
                cursor,
                outfitsRepository,
                realmData,
                webController,
                equippedWearables,
                wearableStorage,
                wearablesProvider,
                nftNamesProvider,
                eventBus,
                deleteIcon,
                decentralandUrlsSource
            );
        }

        public void Dispose()
        {
            busController?.Dispose();
            backpackController?.Dispose();
            backpackEquipStatusController?.Dispose();
        }
    }
}
