using CommunicationData.URLHelpers;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.BackpackBus;
using Runtime.Wearables;

namespace DCL.Backpack.AvatarSection.Outfits
{
    public class OutfitApplier
    {
        private readonly BackpackCommandBus bus;

        public OutfitApplier(BackpackCommandBus bus)
        {
            this.bus = bus;
        }

        public void Apply(Outfit outfit)
        {
            bus.SendCommand(new BackpackUnEquipAllWearablesCommand());

            if (!string.IsNullOrEmpty(outfit.bodyShape))
                bus.SendCommand(new BackpackEquipWearableCommand(new URN(outfit.bodyShape).Shorten()));

            foreach (string wearableId in outfit.wearables)
                bus.SendCommand(new BackpackEquipWearableCommand(new URN(wearableId).Shorten()));

            bus.SendCommand(new BackpackChangeColorCommand(outfit.hair.color,
                WearableCategories.Categories.HAIR));

            bus.SendCommand(new BackpackChangeColorCommand(outfit.eyes.color,
                WearableCategories.Categories.EYES));

            bus.SendCommand(new BackpackChangeColorCommand(outfit.skin.color,
                WearableCategories.Categories.BODY_SHAPE));
        }
    }
}