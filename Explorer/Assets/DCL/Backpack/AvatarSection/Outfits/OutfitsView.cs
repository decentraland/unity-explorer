using DCL.Backpack.AvatarSection.Outfits.Banner;
using DCL.Backpack.AvatarSection.Outfits.Slots;
using DCL.UI;
using UnityEngine;

namespace DCL.Backpack.AvatarSection.Outfits
{
    public class OutfitsView : MonoBehaviour
    {
        public SearchBarView BackpackSearchBar;
        public BackpackSortDropdownView BackpackSortDropdown;

        [Header("Slot Containers")]
        [SerializeField] public OutfitSlotView[] BaseOutfitSlots;

        [SerializeField] public GameObject ExtraSlotsContainer;
        [SerializeField] public OutfitSlotView[] ExtraOutfitSlots;

        [Header("Banner")]
        [SerializeField] public OutfitBannerView OutfitsBanner;
    }
}