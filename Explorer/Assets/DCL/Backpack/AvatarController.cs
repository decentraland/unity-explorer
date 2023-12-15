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

        public AvatarController(AvatarView view,
            AvatarSlotView[] slotViews,
            NftTypeIconSO rarityBackgrounds,
            NftTypeIconSO categoryIcons,
            NFTColorsSO rarityColors)
        {
            this.view = view;
            this.rarityBackgrounds = rarityBackgrounds;
            this.categoryIcons = categoryIcons;
            this.rarityColors = rarityColors;

            slotsController = new BackpackSlotsController(slotViews);
            rectTransform = view.GetComponent<RectTransform>();
        }

        public void Activate() { }

        public void Deactivate() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
