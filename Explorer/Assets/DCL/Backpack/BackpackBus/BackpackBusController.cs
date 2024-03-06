using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.CharacterPreview;
using DCL.Diagnostics;
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
            this.backpackCommandBus.OnFilterCategoryMessageReceived += HandleFilterCategoryCommand;
            this.backpackCommandBus.OnSearchMessageReceived += HandleSearchCommand;
            this.backpackCommandBus.OnPublishProfileReceived += HandlePublishProfile;
        }

        private void HandlePublishProfile(BackpackPublishProfileCommand command)
        {
            backpackEventBus.SendPublishProfile();
        }

        private void HandleSearchCommand(BackpackSearchCommand command)
        {
            if (!string.IsNullOrEmpty(command.SearchText))
                backpackEventBus.SendFilterCategory(string.Empty, AvatarWearableCategoryEnum.Body);

            backpackEventBus.SendSearch(command.SearchText);
        }

        private void HandleSelectCommand(BackpackSelectCommand command)
        {
            if (wearableCatalog.TryGetWearable(command.Id, out IWearable wearable))
                backpackEventBus.SendSelect(wearable);
        }

        private void HandleFilterCategoryCommand(BackpackFilterCategoryCommand command)
        {
            backpackEventBus.SendSearch(string.Empty);
            backpackEventBus.SendFilterCategory(command.Category, command.CategoryEnum);
        }

        private void HandleEquipCommand(BackpackEquipCommand command)
        {
            wearableCatalog.TryGetWearable(command.Id, out IWearable? wearable);

            if (wearable == null)
            {
                ReportHub.LogError(new ReportData(ReportCategory.WEARABLE), $"Cannot equip wearable, not found: {command.Id}");
                return;
            }

            string? category = wearable.GetCategory();

            if (category == null)
            {
                ReportHub.LogError(new ReportData(ReportCategory.WEARABLE), $"Cannot equip wearable, category is invalid: {command.Id}");
                return;
            }

            IWearable? wearableToUnequip = backpackEquipStatusController.GetEquippedWearableForCategory(category);

            if (wearableToUnequip != null)
                backpackEventBus.SendUnEquip(wearableToUnequip);

            backpackEventBus.SendEquip(wearable);
        }

        private void HandleUnEquipCommand(BackpackUnEquipCommand command)
        {
            if (!wearableCatalog.TryGetWearable(command.Id, out IWearable wearable))
                wearableCatalog.TryGetWearable(new URN(command.Id).Shorten(), out wearable);

            backpackEventBus.SendUnEquip(wearable);
        }

        private void HandleHideCommand(BackpackHideCommand command)
        {
            backpackEventBus.SendForceRender(command.ForceRender);
        }

        public void Dispose()
        {
            backpackCommandBus.OnEquipMessageReceived -= HandleEquipCommand;
            backpackCommandBus.OnUnEquipMessageReceived -= HandleUnEquipCommand;
            backpackCommandBus.OnHideMessageReceived -= HandleHideCommand;
            backpackCommandBus.OnSelectMessageReceived -= HandleSelectCommand;
            backpackCommandBus.OnFilterCategoryMessageReceived -= HandleFilterCategoryCommand;
            backpackCommandBus.OnSearchMessageReceived -= HandleSearchCommand;
        }
    }
}
