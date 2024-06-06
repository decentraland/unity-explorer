using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.CharacterPreview;
using DCL.Backpack.EmotesSection;
using DCL.CharacterPreview;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.WebRequests;
using UnityEngine.Pool;

namespace DCL.PluginSystem.Global
{
    internal class BackpackSubPlugin : IDisposable
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWearableCatalog wearableCatalog;
        private readonly IEquippedWearables equippedWearables;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IEmoteCache emoteCache;
        private readonly IReadOnlyCollection<URN> embeddedEmotes;
        private readonly IRealmData realmData;
        private readonly DCLInput dclInput;
        private readonly IWeb3IdentityCache web3Identity;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackEventBus backpackEventBus;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly BackpackEquipStatusController backpackEquipStatusController;
        private readonly URLDomain assetBundleURL;
        private readonly IWebRequestController webRequestController;

        private BackpackBusController? busController;
        private Arch.Core.World? world;
        private Entity? playerEntity;

        internal BackpackController? backpackController { get; private set; }

        public BackpackSubPlugin(
            IAssetsProvisioner assetsProvisioner,
            IWeb3IdentityCache web3Identity,
            ICharacterPreviewFactory characterPreviewFactory,
            IWearableCatalog wearableCatalog,
            ISelfProfile selfProfile,
            IEquippedWearables equippedWearables,
            IEquippedEmotes equippedEmotes,
            IEmoteCache emoteCache,
            IReadOnlyCollection<URN> embeddedEmotes,
            ICollection<string> forceRender,
            IRealmData realmData,
            DCLInput dclInput, URLDomain assetBundleURL, IWebRequestController webRequestController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.web3Identity = web3Identity;
            this.characterPreviewFactory = characterPreviewFactory;
            this.wearableCatalog = wearableCatalog;
            this.equippedWearables = equippedWearables;
            this.equippedEmotes = equippedEmotes;
            this.emoteCache = emoteCache;
            this.embeddedEmotes = embeddedEmotes;
            this.realmData = realmData;
            this.dclInput = dclInput;
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;

            backpackCommandBus = new BackpackCommandBus();
            backpackEventBus = new BackpackEventBus();

            backpackEquipStatusController = new BackpackEquipStatusController(
                backpackEventBus,
                equippedEmotes,
                equippedWearables,
                selfProfile,
                forceRender,
                ProvideEcsContext
            );
        }

        internal async UniTask<ContinueInitialization> InitializeAsync(
            BackpackSettings backpackSettings,
            BackpackView view,
            CancellationToken ct)
        {
            // Initialize assets that do not require World
            var sortController = new BackpackSortController(view.BackpackSortView);

            busController = new BackpackBusController(wearableCatalog, backpackEventBus, backpackCommandBus, equippedWearables, equippedEmotes, emoteCache);

            (NFTColorsSO rarityColorMappings, NftTypeIconSO categoryIconsMapping, NftTypeIconSO rarityBackgroundsMapping, NftTypeIconSO rarityInfoPanelBackgroundsMapping) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityColorMappings, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.CategoryIconsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityBackgroundsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityInfoPanelBackgroundsMapping, ct));

            PageButtonView pageButtonView = (await assetsProvisioner.ProvideMainAssetAsync(backpackSettings.PageButtonView, ct)).Value.GetComponent<PageButtonView>().EnsureNotNull();

            AvatarView avatarView = view.GetComponentInChildren<AvatarView>().EnsureNotNull();

            var wearableInfoPanelController = new BackpackInfoPanelController(
                avatarView.backpackInfoPanelView,
                backpackEventBus,
                categoryIconsMapping,
                rarityInfoPanelBackgroundsMapping,
                rarityColorMappings,
                equippedWearables,
                BackpackInfoPanelController.AttachmentType.Wearable
            );

            EmotesView emoteView = view.GetComponentInChildren<EmotesView>().EnsureNotNull();

            BackpackInfoPanelController emoteInfoPanelController = new BackpackInfoPanelController(
                emoteView.BackpackInfoPanelView,
                backpackEventBus,
                categoryIconsMapping,
                rarityInfoPanelBackgroundsMapping,
                rarityColorMappings,
                equippedWearables,
                BackpackInfoPanelController.AttachmentType.Emote
            );

            //not injected anywhere
            var _ = new BackpackEmoteBreadCrumbController(emoteView.BreadCrumb, backpackEventBus);

            ObjectPool<BackpackEmoteGridItemView>? emoteGridPool = await BackpackEmoteGridController.InitializeAssetsAsync(assetsProvisioner, emoteView.GridView, ct);

            await wearableInfoPanelController.InitialiseAssetsAsync(assetsProvisioner, ct);

            ObjectPool<BackpackItemView>? gridPool = await BackpackGridController.InitialiseAssetsAsync(assetsProvisioner, avatarView.backpackGridView, ct);

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments args) =>
            {
                world = builder.World!;
                playerEntity = args.PlayerEntity;

                var thumbnailProvider = new ECSThumbnailProvider(realmData, builder.World, assetBundleURL, webRequestController);

                var gridController = new BackpackGridController(
                    avatarView.backpackGridView, backpackCommandBus, backpackEventBus,
                    web3Identity, rarityBackgroundsMapping, rarityColorMappings, categoryIconsMapping,
                    equippedWearables, sortController, pageButtonView, gridPool, world,
                    thumbnailProvider
                );

                var emoteGridController = new BackpackEmoteGridController(emoteView.GridView, backpackCommandBus, backpackEventBus,
                    web3Identity, rarityBackgroundsMapping, rarityColorMappings, categoryIconsMapping, equippedEmotes,
                    sortController, pageButtonView, emoteGridPool, args.EmoteProvider, embeddedEmotes, thumbnailProvider);

                var emotesController = new EmotesController(emoteView,
                    new BackpackEmoteSlotsController(emoteView.Slots, backpackEventBus, backpackCommandBus, rarityBackgroundsMapping), emoteGridController);

                var backpackCharacterPreviewController = new BackpackCharacterPreviewController(view.CharacterPreviewView,
                    characterPreviewFactory, backpackEventBus, world, equippedEmotes);

                backpackController = new BackpackController(
                    view,
                    avatarView,
                    rarityBackgroundsMapping,
                    backpackCommandBus,
                    backpackEventBus,
                    gridController,
                    wearableInfoPanelController,
                    emoteInfoPanelController,
                    world,
                    args.PlayerEntity,
                    emoteGridController,
                    avatarView.GetComponentsInChildren<AvatarSlotView>().EnsureNotNull(),
                    emotesController,
                    backpackCharacterPreviewController,
                    thumbnailProvider,
                    dclInput
                );
            };
        }

        public void Dispose()
        {
            busController?.Dispose();
            backpackController?.Dispose();
            backpackEquipStatusController?.Dispose();
        }

        private (Arch.Core.World, Entity) ProvideEcsContext() =>
            (world!, playerEntity!.Value);
    }
}
