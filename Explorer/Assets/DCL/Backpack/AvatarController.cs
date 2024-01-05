using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack.BackpackBus;
using DCL.UI;
using DCL.Web3Authentication.Identities;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Backpack
{
    public class AvatarController : ISection, IDisposable
    {
        private readonly RectTransform rectTransform;
        private readonly AvatarView view;
        private readonly BackpackSlotsController slotsController;
        private readonly BackpackGridController backpackGridController;
        private readonly BackpackInfoPanelController backpackInfoPanelController;

        public AvatarController(AvatarView view,
            AvatarSlotView[] slotViews,
            NftTypeIconSO rarityBackgrounds,
            NftTypeIconSO rarityInfoPanelBackgrounds,
            NftTypeIconSO categoryIcons,
            NFTColorsSO rarityColors,
            BackpackCommandBus backpackCommandBus,
            BackpackEventBus backpackEventBus,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.view = view;

            slotsController = new BackpackSlotsController(slotViews, backpackCommandBus, backpackEventBus, rarityBackgrounds);
            backpackGridController = new BackpackGridController(view.backpackGridView, backpackCommandBus, backpackEventBus, web3IdentityCache, rarityBackgrounds, rarityColors, categoryIcons);
            backpackInfoPanelController = new BackpackInfoPanelController(view.backpackInfoPanelView, backpackEventBus, categoryIcons, rarityInfoPanelBackgrounds);
            rectTransform = view.GetComponent<RectTransform>();
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct) =>
            await backpackGridController.InitialiseAssetsAsync(assetsProvisioner, ct);

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in Entity playerEntity)
        {
            backpackGridController.InjectToWorld(ref builder, playerEntity);
        }

        public void RequestInitialWearablesPage()
        {
            backpackGridController.RequestPage(0);
        }

        public void Activate()
        {
        }

        public void Deactivate() { }

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void Dispose()
        {
            slotsController?.Dispose();
            backpackInfoPanelController?.Dispose();
        }
    }
}
