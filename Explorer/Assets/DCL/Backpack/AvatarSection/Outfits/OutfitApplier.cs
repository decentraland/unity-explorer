using DCL.Backpack.AvatarSection.Outfits.Commands;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.BackpackBus;

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
            var command = new BackpackEquipOutfitCommand(
                outfit.bodyShape,
                outfit.wearables,
                outfit.eyes.color,
                outfit.hair.color,
                outfit.skin.color,
                outfit.forceRender
            );

            bus.SendCommand(command);
        }
    }
}
