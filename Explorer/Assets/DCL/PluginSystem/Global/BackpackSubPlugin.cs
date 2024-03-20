using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.Profiles;
using DCL.UI;
using DCL.Web3.Identities;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.PluginSystem.Global
{
    internal class BackpackSubPlugin
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWearableCatalog wearableCatalog;
        private readonly IProfileRepository profileRepository;
        private readonly IEmoteCache emoteCache;
        private readonly IWeb3IdentityCache web3Identity;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackEventBus backpackEventBus;
        private readonly ICharacterPreviewFactory characterPreviewFactory;

        private BackpackBusController? busController;
        private Arch.Core.World? world;
        private Entity? playerEntity;

        internal BackpackController? backpackController { get; private set; }

        public BackpackSubPlugin(IAssetsProvisioner assetsProvisioner, IWeb3IdentityCache web3Identity,
            ICharacterPreviewFactory characterPreviewFactory, IWearableCatalog wearableCatalog,
            IProfileRepository profileRepository,
            IEmoteCache emoteCache)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.web3Identity = web3Identity;
            this.characterPreviewFactory = characterPreviewFactory;
            this.wearableCatalog = wearableCatalog;
            this.profileRepository = profileRepository;
            this.emoteCache = emoteCache;

            backpackCommandBus = new BackpackCommandBus();
            backpackEventBus = new BackpackEventBus();
        }

        internal async UniTask<ContinueInitialization> InitializeAsync(
            BackpackSettings backpackSettings,
            BackpackView view,
            CancellationToken ct)
        {
            // Initialize assets that do not require World
            var sortController = new BackpackSortController(view.BackpackSortView);
            var backpackEquipStatusController = new BackpackEquipStatusController(backpackEventBus, profileRepository, web3Identity, wearableCatalog, emoteCache, ProvideEcsContext);

            busController = new BackpackBusController(wearableCatalog, backpackEventBus, backpackCommandBus, backpackEquipStatusController, emoteCache);

            (NFTColorsSO rarityColorMappings, NftTypeIconSO categoryIconsMapping, NftTypeIconSO rarityBackgroundsMapping, NftTypeIconSO rarityInfoPanelBackgroundsMapping) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityColorMappings, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.CategoryIconsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityBackgroundsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityInfoPanelBackgroundsMapping, ct));

            PageButtonView? pageButtonView = (await assetsProvisioner.ProvideMainAssetAsync(backpackSettings.PageButtonView, ct)).Value.GetComponent<PageButtonView>();

            AvatarView? avatarView = view.GetComponentInChildren<AvatarView>();

            BackpackInfoPanelController wearableInfoPanelController = new (avatarView.backpackInfoPanelView, backpackEventBus,
                categoryIconsMapping, rarityInfoPanelBackgroundsMapping, rarityColorMappings, backpackEquipStatusController,
                BackpackInfoPanelController.AttachmentType.Wearable);

            EmotesView emoteView = view.GetComponentInChildren<EmotesView>();

            BackpackInfoPanelController emoteInfoPanelController = new (emoteView.BackpackInfoPanelView, backpackEventBus,
                categoryIconsMapping, rarityInfoPanelBackgroundsMapping, rarityColorMappings, backpackEquipStatusController,
                BackpackInfoPanelController.AttachmentType.Emote);

            ObjectPool<BackpackItemView>? emoteGridPool = await BackpackEmoteGridController.InitializeAssetsAsync(assetsProvisioner, emoteView.GridView, ct);

            await wearableInfoPanelController.InitialiseAssetsAsync(assetsProvisioner, ct);

            ObjectPool<BackpackItemView>? gridPool = await BackpackGridController.InitialiseAssetsAsync(assetsProvisioner, avatarView.backpackGridView, ct);

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments args) =>
            {
                world = builder.World;
                playerEntity = args.PlayerEntity;

                var gridController = new BackpackGridController(avatarView.backpackGridView, backpackCommandBus, backpackEventBus,
                    web3Identity, rarityBackgroundsMapping, rarityColorMappings, categoryIconsMapping,
                    backpackEquipStatusController, sortController, pageButtonView, gridPool, builder.World);

                var emoteGridController = new BackpackEmoteGridController(emoteView.GridView, backpackCommandBus, backpackEventBus,
                    web3Identity, rarityBackgroundsMapping, rarityColorMappings, categoryIconsMapping, backpackEquipStatusController,
                    sortController, pageButtonView, emoteGridPool, args.EmoteProvider);

                var emotesController = new EmotesController(view.GetComponentInChildren<EmotesView>(),
                    new BackpackEmoteSlotsController(emoteView.Slots, backpackEventBus, rarityInfoPanelBackgroundsMapping));

                backpackController = new BackpackController(view, avatarView, rarityInfoPanelBackgroundsMapping, backpackCommandBus, backpackEventBus,
                    characterPreviewFactory, gridController, wearableInfoPanelController, emoteInfoPanelController, builder.World, args.PlayerEntity,
                    emoteGridController, avatarView.GetComponentsInChildren<AvatarSlotView>(), emotesController);
            };
        }

        public void Dispose()
        {
            busController?.Dispose();
            backpackController?.Dispose();
        }

        private (Arch.Core.World, Entity) ProvideEcsContext() =>
            (world!, playerEntity!.Value);
    }
}
