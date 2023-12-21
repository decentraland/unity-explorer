using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using JetBrains.Annotations;
using System;

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

            backpackCommandBus.OnMessageReceived += HandleBackpackMessageReceived;
        }

        private void HandleBackpackMessageReceived(BackpackCommand command)
        {
            switch (command.Type)
            {
                case BackpackCommandType.EquipCommand:
                    HandleEquipCommand(command.Id);
                    break;
                case BackpackCommandType.UnequipCommand:
                    HandleUnEquipCommand(command.Id, command.Category);
                    break;
                case BackpackCommandType.HideCommand:
                    HandleHideCommand();
                    break;
                case BackpackCommandType.SelectCommand:
                    HandleSelectCommand(command.Id);
                    break;
            }
        }

        private void HandleSelectCommand(string wearableId)
        {
            if (wearableCatalog.TryGetWearable(wearableId, out IWearable wearable))
            {
                backpackEventBus.SendSelect(wearable);
            }
        }

        private void HandleEquipCommand(string wearableId)
        {
            if (wearableCatalog.TryGetWearable(wearableId, out IWearable wearable))
            {
                backpackEventBus.SendEquip(wearable);
            }
        }

        private void HandleUnEquipCommand(string wearableId, string category)
        {
            if (!string.IsNullOrEmpty(wearableId))
            {

            }
            if (!string.IsNullOrEmpty(category))
            {

            }
        }

        private void HandleHideCommand()
        {

        }
    }
}
