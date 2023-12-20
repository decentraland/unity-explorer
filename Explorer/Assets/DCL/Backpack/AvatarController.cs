using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack.BackpackBus;
using DCL.UI;
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
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NftTypeIconSO categoryIcons;
        private readonly NFTColorsSO rarityColors;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackEventBus backpackEventBus;
        private readonly BackpackGridController backpackGridController;
        private readonly BackpackInfoPanelController backpackInfoPanelController;

        public AvatarController(AvatarView view,
            AvatarSlotView[] slotViews,
            NftTypeIconSO rarityBackgrounds,
            NftTypeIconSO categoryIcons,
            NFTColorsSO rarityColors,
            BackpackCommandBus backpackCommandBus,
            BackpackEventBus backpackEventBus)
        {
            this.view = view;
            this.rarityBackgrounds = rarityBackgrounds;
            this.categoryIcons = categoryIcons;
            this.rarityColors = rarityColors;
            this.backpackCommandBus = backpackCommandBus;
            this.backpackEventBus = backpackEventBus;

            slotsController = new BackpackSlotsController(slotViews, backpackCommandBus, backpackEventBus);
            backpackGridController = new BackpackGridController(view.backpackGridView, backpackCommandBus, backpackEventBus);
            backpackInfoPanelController = new BackpackInfoPanelController(view.backpackInfoPanelView);
            rectTransform = view.GetComponent<RectTransform>();
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct) =>
            await backpackGridController.InitialiseAssetsAsync(assetsProvisioner, ct);

        public void Activate() { }

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
