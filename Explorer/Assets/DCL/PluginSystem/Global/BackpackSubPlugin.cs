using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
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
        private readonly IWeb3IdentityCache web3Identity;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackEventBus backpackEventBus;
        private readonly ICharacterPreviewFactory characterPreviewFactory;

        private BackpackBusController? busController;
        internal BackpackController? backpackController { get; private set; }

        public BackpackSubPlugin(IAssetsProvisioner assetsProvisioner, IWeb3IdentityCache web3Identity, ICharacterPreviewFactory characterPreviewFactory, IWearableCatalog wearableCatalog)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.web3Identity = web3Identity;
            this.characterPreviewFactory = characterPreviewFactory;
            this.wearableCatalog = wearableCatalog;

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
            var backpackEquipStatusController = new BackpackEquipStatusController(backpackEventBus);

            busController = new BackpackBusController(wearableCatalog, backpackEventBus, backpackCommandBus, backpackEquipStatusController);

            (NFTColorsSO rarityColorMappings, NftTypeIconSO categoryIconsMapping, NftTypeIconSO rarityBackgroundsMapping, NftTypeIconSO rarityInfoPanelBackgroundsMapping) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityColorMappings, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.CategoryIconsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityBackgroundsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(backpackSettings.RarityInfoPanelBackgroundsMapping, ct));

            PageButtonView? pageButtonView = (await assetsProvisioner.ProvideMainAssetAsync(backpackSettings.PageButtonView, ct)).Value.GetComponent<PageButtonView>();

            AvatarView? avatarView = view.GetComponentInChildren<AvatarView>();

            var infoPanelController = new BackpackInfoPanelController(avatarView.backpackInfoPanelView, backpackEventBus,
                categoryIconsMapping, rarityInfoPanelBackgroundsMapping, rarityColorMappings, backpackEquipStatusController);

            await infoPanelController.InitialiseAssetsAsync(assetsProvisioner, ct);

            ObjectPool<BackpackItemView>? gridPool = await BackpackGridController.InitialiseAssetsAsync(assetsProvisioner, avatarView.backpackGridView, ct);

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> world, in GlobalPluginArguments args) =>
            {
                var gridController = new BackpackGridController(avatarView.backpackGridView, backpackCommandBus, backpackEventBus,
                    web3Identity, rarityBackgroundsMapping, rarityColorMappings, categoryIconsMapping,
                    backpackEquipStatusController, sortController, pageButtonView, gridPool, world.World);

                backpackController = new BackpackController(view, avatarView, rarityInfoPanelBackgroundsMapping, backpackCommandBus, backpackEventBus,
                    characterPreviewFactory, gridController, infoPanelController, world.World, args.PlayerEntity);
            };
        }

        public void Dispose()
        {
            busController?.Dispose();
            backpackController?.Dispose();
        }
    }
}
