using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using System;

namespace DCL.Backpack
{
    public class BackpackGridController
    {
        private readonly BackpackCommandBus commandBus;
        private readonly BackpackEventBus eventBus;

        public BackpackGridController(BackpackCommandBus commandBus, BackpackEventBus eventBus)
        {
            this.commandBus = commandBus;
            this.eventBus = eventBus;

            eventBus.EquipEvent += OnEquip;
            eventBus.UnEquipEvent += OnUnequip;
        }

        private void OnUnequip(IWearable unequippedWearable)
        {

        }

        private void OnEquip(IWearable equippedWearable)
        {

        }
    }
}
