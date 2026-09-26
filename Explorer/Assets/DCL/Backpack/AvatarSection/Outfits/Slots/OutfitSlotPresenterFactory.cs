using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Backpack.Slots;

namespace DCL.Backpack.AvatarSection.Outfits.Slots
{
    public class OutfitSlotPresenterFactory
    {
        private readonly IAvatarScreenshotService screenshotService;

        public OutfitSlotPresenterFactory(IAvatarScreenshotService screenshotService)
        {
            this.screenshotService = screenshotService;
        }

        public OutfitSlotPresenter Create(OutfitSlotView slotView, int slotIndex)
        {
            return new OutfitSlotPresenter(slotView, slotIndex, screenshotService);
        }
    }
}