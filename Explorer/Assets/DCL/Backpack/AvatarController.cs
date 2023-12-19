using DCL.Backpack.BackpackBus;
using DCL.UI;
using UnityEngine;

namespace DCL.Backpack
{
    public class AvatarController : ISection
    {
        private readonly RectTransform rectTransform;
        private readonly AvatarView view;
        private readonly BackpackSlotsController slotsController;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NftTypeIconSO categoryIcons;
        private readonly NFTColorsSO rarityColors;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackEventBus backpackEventBus;

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
            rectTransform = view.GetComponent<RectTransform>();
        }

        public void Activate() { }

        public void Deactivate() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
