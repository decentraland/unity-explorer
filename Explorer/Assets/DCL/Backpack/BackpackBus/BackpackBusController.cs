using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using System;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackBusController : IDisposable
    {
        private readonly IWearableCatalog wearableCatalog;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IBackpackCommandBus backpackCommandBus;
        private readonly IBackpackEquipStatusController backpackEquipStatusController;

        public BackpackBusController(
            IWearableCatalog wearableCatalog,
            IBackpackEventBus backpackEventBus,
            IBackpackCommandBus backpackCommandBus,
            IBackpackEquipStatusController backpackEquipStatusController)
        {
            this.wearableCatalog = wearableCatalog;
            this.backpackEventBus = backpackEventBus;
            this.backpackCommandBus = backpackCommandBus;
            this.backpackEquipStatusController = backpackEquipStatusController;

            this.backpackCommandBus.OnEquipMessageReceived += HandleEquipCommand;
            this.backpackCommandBus.OnUnEquipMessageReceived += HandleUnEquipCommand;
            this.backpackCommandBus.OnHideMessageReceived += HandleHideCommand;
            this.backpackCommandBus.OnSelectMessageReceived += HandleSelectCommand;
        }

        private void HandleSelectCommand(BackpackSelectCommand command)
        {
            if (wearableCatalog.TryGetWearable(command.Id, out IWearable wearable))
                backpackEventBus.SendSelect(wearable);
        }

        private void HandleEquipCommand(BackpackEquipCommand command)
        {
            wearableCatalog.TryGetWearable(command.Id, out IWearable wearable);
            string category = wearable?.GetCategory();

            if (category == null)
                return;

            if (backpackEquipStatusController.GetEquippedWearableForCategory(category) != null)
                backpackEventBus.SendUnEquip(backpackEquipStatusController.GetEquippedWearableForCategory(category));

            backpackEventBus.SendEquip(wearable);
        }

        private void HandleUnEquipCommand(BackpackUnEquipCommand command)
        {
            if (wearableCatalog.TryGetWearable(command.Id, out IWearable wearable))
                backpackEventBus.SendUnEquip(wearable);
        }

        private void HandleHideCommand(BackpackHideCommand command) { }

        public void Dispose()
        {
            backpackCommandBus.OnEquipMessageReceived -= HandleEquipCommand;
            backpackCommandBus.OnUnEquipMessageReceived -= HandleUnEquipCommand;
            backpackCommandBus.OnHideMessageReceived -= HandleHideCommand;
            backpackCommandBus.OnSelectMessageReceived -= HandleSelectCommand;
        }
    }
}
