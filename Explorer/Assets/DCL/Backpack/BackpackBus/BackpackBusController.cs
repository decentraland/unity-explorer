using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
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
        private readonly IEmoteCache emoteCache;

        public BackpackBusController(
            IWearableCatalog wearableCatalog,
            IBackpackEventBus backpackEventBus,
            IBackpackCommandBus backpackCommandBus,
            IBackpackEquipStatusController backpackEquipStatusController,
            IEmoteCache emoteCache)
        {
            this.wearableCatalog = wearableCatalog;
            this.backpackEventBus = backpackEventBus;
            this.backpackCommandBus = backpackCommandBus;
            this.backpackEquipStatusController = backpackEquipStatusController;
            this.emoteCache = emoteCache;

            this.backpackCommandBus.EquipWearableMessageReceived += HandleEquipWearableCommand;
            this.backpackCommandBus.UnEquipWearableMessageReceived += HandleUnEquipWearableCommand;
            this.backpackCommandBus.EquipEmoteMessageReceived += HandleEmoteEquipCommand;
            this.backpackCommandBus.UnEquipEmoteMessageReceived += HandleUnEquipEmoteCommand;
            this.backpackCommandBus.HideMessageReceived += HandleHideCommand;
            this.backpackCommandBus.SelectWearableMessageReceived += HandleSelectWearableCommand;
            this.backpackCommandBus.SelectEmoteMessageReceived += HandleSelectEmoteCommand;
            this.backpackCommandBus.FilterCategoryMessageReceived += HandleFilterCategoryCommand;
            this.backpackCommandBus.SearchMessageReceived += HandleSearchCommand;
            this.backpackCommandBus.PublishProfileReceived += HandlePublishProfile;
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

        private void HandleSelectWearableCommand(BackpackSelectWearableCommand command)
        {
            if (wearableCatalog.TryGetWearable(command.Id, out IWearable wearable))
                backpackEventBus.SendWearableSelect(wearable);
        }

        private void HandleFilterCategoryCommand(BackpackFilterCategoryCommand command)
        {
            backpackEventBus.SendSearch(string.Empty);
            backpackEventBus.SendFilterCategory(command.Category, command.CategoryEnum);
        }

        private void HandleEquipWearableCommand(BackpackEquipWearableCommand command)
        {
            if (!wearableCatalog.TryGetWearable(command.Id, out IWearable wearable))
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
                backpackEventBus.SendUnEquipWearable(wearableToUnequip);

            backpackEventBus.SendEquipWearable(wearable);
        }

        private void HandleEmoteEquipCommand(BackpackEquipEmoteCommand command)
        {
            if (!emoteCache.TryGetEmote(command.Id, out IEmote emote))
            {
                ReportHub.LogError(new ReportData(ReportCategory.EMOTE), $"Cannot equip emote, not found: {command.Id}");
                return;
            }

            if (command.Slot is < 0 or >= 10)
            {
                ReportHub.LogError(new ReportData(ReportCategory.EMOTE), $"Cannot equip emote, slot out of bounds: {command.Id} - {command.Slot}");
                return;
            }

            backpackEventBus.SendUnEquipEmote(command.Slot, backpackEquipStatusController.GetEquippedEmote(command.Slot));
            backpackEventBus.SendEquipEmote(command.Slot, emote);
        }

        private void HandleUnEquipWearableCommand(BackpackUnEquipWearableCommand command)
        {
            if (!wearableCatalog.TryGetWearable(command.Id, out IWearable wearable))
                wearableCatalog.TryGetWearable(new URN(command.Id).Shorten(), out wearable);

            backpackEventBus.SendUnEquipWearable(wearable);
        }

        private void HandleUnEquipEmoteCommand(BackpackUnEquipEmoteCommand command)
        {
            backpackEventBus.SendUnEquipEmote(command.Slot, backpackEquipStatusController.GetEquippedEmote(command.Slot));
        }

        private void HandleSelectEmoteCommand(BackpackSelectEmoteCommand command)
        {
            if (emoteCache.TryGetEmote(command.Id, out IEmote emote))
                backpackEventBus.SendEmoteSelect(emote);
        }

        private void HandleHideCommand(BackpackHideCommand command)
        {
            backpackEventBus.SendForceRender(command.ForceRender);
        }

        public void Dispose()
        {
            backpackCommandBus.EquipWearableMessageReceived -= HandleEquipWearableCommand;
            backpackCommandBus.UnEquipWearableMessageReceived -= HandleUnEquipWearableCommand;
            backpackCommandBus.HideMessageReceived -= HandleHideCommand;
            backpackCommandBus.SelectWearableMessageReceived -= HandleSelectWearableCommand;
            backpackCommandBus.FilterCategoryMessageReceived -= HandleFilterCategoryCommand;
            backpackCommandBus.SearchMessageReceived -= HandleSearchCommand;
        }
    }
}
