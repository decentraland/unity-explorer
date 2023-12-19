using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackBusController
    {
        private readonly IWearableCatalog wearableCatalog;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IBackpackCommandBus backpackCommandBus;

        public BackpackBusController(IWearableCatalog wearableCatalog, IBackpackEventBus backpackEventBus, IBackpackCommandBus backpackCommandBus)
        {
            this.wearableCatalog = wearableCatalog;
            this.backpackEventBus = backpackEventBus;
            this.backpackCommandBus = backpackCommandBus;
        }

        public void Equip(EquipCommand equipCommand)
        {
            if (wearableCatalog.TryGetWearable(equipCommand.Id, out IWearable equipWearable))
            {

            }
        }
    }
}
