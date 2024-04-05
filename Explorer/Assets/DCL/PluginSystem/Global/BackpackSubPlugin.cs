using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.Profiles;
using DCL.Profiles.Publishing;
using DCL.UI;
using DCL.Utilities.Extensions;
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
            IProfileRepository profileRepository)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.web3Identity = web3Identity;
            this.characterPreviewFactory = characterPreviewFactory;
            this.wearableCatalog = wearableCatalog;
            this.profileRepository = profileRepository;

            backpackCommandBus = new BackpackCommandBus();
            backpackEventBus = new BackpackEventBus();
        }

        internal async UniTask<ContinueInitialization> InitializeAsync(
            BackpackSettings backpackSettings,
            BackpackView view,
            CancellationToken ct)
        {
            // Initialize assets that do not require World
            var equippedWearables = new EquippedWearables();
            var sortController = new BackpackSortController(view.BackpackSortView);
            var profilePublishing = new ProfilePublishing(profileRepository, web3Identity, equippedWearables, wearableCatalog);

            //TODO after refactor this object is unused at all, remove?
            var backpackEquipStatusController = new BackpackEquipStatusController(
                backpackEventBus,
                profileRepository,
                web3Identity,
                equippedWearables,
                profilePublishing,
                ProvideEcsContext
            );

            busController = new BackpackBusController(wearableCatalog, backpackEventBus, backpackCommandBus, equippedWearables);

            (NFTColorsSO rarityColorMappings, NftTypeIconSO categoryIconsMapping, NftTypeIconSO rarityBackgroundsMapping, NftTypeIconSO rarityInfoPanelBackgroundsMapping) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityColorMappings, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.CategoryIconsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityBackgroundsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityInfoPanelBackgroundsMapping, ct));

            PageButtonView pageButtonView = (await assetsProvisioner.ProvideMainAssetAsync(backpackSettings.PageButtonView, ct)).Value.GetComponent<PageButtonView>().EnsureNotNull();

            AvatarView avatarView = view.GetComponentInChildren<AvatarView>().EnsureNotNull();

            var infoPanelController = new BackpackInfoPanelController(avatarView.backpackInfoPanelView, backpackEventBus,
                categoryIconsMapping, rarityInfoPanelBackgroundsMapping, rarityColorMappings, equippedWearables);

            await infoPanelController.InitialiseAssetsAsync(assetsProvisioner, ct);

            ObjectPool<BackpackItemView>? gridPool = await BackpackGridController.InitialiseAssetsAsync(assetsProvisioner, avatarView.backpackGridView, ct);

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments args) =>
            {
                world = builder.World!;
                playerEntity = args.PlayerEntity;

                var gridController = new BackpackGridController(
                    avatarView.backpackGridView, backpackCommandBus, backpackEventBus,
                    web3Identity, rarityBackgroundsMapping, rarityColorMappings, categoryIconsMapping,
                    equippedWearables, sortController, pageButtonView, gridPool, world
                );

                backpackController = new BackpackController(view, avatarView, rarityInfoPanelBackgroundsMapping, backpackCommandBus, backpackEventBus,
                    characterPreviewFactory, gridController, infoPanelController, world, args.PlayerEntity);
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
