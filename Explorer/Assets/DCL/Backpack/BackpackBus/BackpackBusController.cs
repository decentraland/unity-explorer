using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackBusController : IDisposable
    {
        private readonly IWearableCatalog wearableCatalog;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IBackpackCommandBus backpackCommandBus;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IEmoteCache emoteCache;

        private int currentEmoteSlot = -1;
        private readonly IReadOnlyEquippedWearables equippedWearables;

        public BackpackBusController(
            IWearableCatalog wearableCatalog,
            IBackpackEventBus backpackEventBus,
            IBackpackCommandBus backpackCommandBus,
            IReadOnlyEquippedWearables equippedWearables,
            IEquippedEmotes equippedEmotes,
            IEmoteCache emoteCache)
        {
            this.wearableCatalog = wearableCatalog;
            this.backpackEventBus = backpackEventBus;
            this.backpackCommandBus = backpackCommandBus;
            this.equippedEmotes = equippedEmotes;
            this.emoteCache = emoteCache;
            this.equippedWearables = equippedWearables;

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
            this.backpackCommandBus.UnEquipAllMessageReceived += HandleUnequipAll;
            this.backpackCommandBus.EmoteSlotSelectMessageReceived += HandleEmoteSlotSelectCommand;
        }

        public void Dispose()
        {
            backpackCommandBus.EquipWearableMessageReceived -= HandleEquipWearableCommand;
            backpackCommandBus.UnEquipWearableMessageReceived -= HandleUnEquipWearableCommand;
            backpackCommandBus.EquipEmoteMessageReceived -= HandleEmoteEquipCommand;
            backpackCommandBus.UnEquipEmoteMessageReceived -= HandleUnEquipEmoteCommand;
            backpackCommandBus.HideMessageReceived -= HandleHideCommand;
            backpackCommandBus.SelectWearableMessageReceived -= HandleSelectWearableCommand;
            backpackCommandBus.SelectEmoteMessageReceived -= HandleSelectEmoteCommand;
            backpackCommandBus.FilterCategoryMessageReceived -= HandleFilterCategoryCommand;
            backpackCommandBus.SearchMessageReceived -= HandleSearchCommand;
            backpackCommandBus.PublishProfileReceived -= HandlePublishProfile;
            this.backpackCommandBus.UnEquipAllMessageReceived -= HandleUnequipAll;
            backpackCommandBus.EmoteSlotSelectMessageReceived -= HandleEmoteSlotSelectCommand;
        }

        private void HandlePublishProfile(BackpackPublishProfileCommand command)
        {
            backpackEventBus.SendPublishProfile();
        }

        private void HandleUnequipAll(BackpackUnEquipAllCommand obj)
        {
            backpackEventBus.SendUnEquipAll();
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

            string? category = null;

            try
            {
                category = wearable.GetCategory();
            }
            catch (Exception)
            {
                // Sometimes the wearable has no available category thus asking for it provokes NRE
            }

            if (category == null)
            {
                ReportHub.LogError(new ReportData(ReportCategory.WEARABLE), $"Cannot equip wearable, category is invalid: {command.Id}");
                return;
            }

            IWearable? wearableToUnequip = equippedWearables.Wearable(category);

            if (wearableToUnequip != null)
                backpackEventBus.SendUnEquipWearable(wearableToUnequip);

            backpackEventBus.SendEquipWearable(wearable);

            if (wearable.Type == WearableType.BodyShape)
                UnEquipIncompatibleWearables(wearable);
        }

        private void UnEquipIncompatibleWearables(IWearable bodyShape)
        {
            List<IWearable> incompatibleWearables = ListPool<IWearable>.Get();

            foreach ((string? _, IWearable? wearable) in equippedWearables.Items())
            {
                if (wearable == null) continue;
                if (wearable == bodyShape) continue;
                if (wearable.IsCompatibleWithBodyShape(bodyShape.GetUrn())) continue;

                // If we send un-equip event here, the equippedWearables list gets modified during this loop throwing an exception in the process
                incompatibleWearables.Add(wearable);
            }

            foreach (IWearable wearable in incompatibleWearables)
                backpackEventBus.SendUnEquipWearable(wearable);

            ListPool<IWearable>.Release(incompatibleWearables);
        }

        private void HandleEmoteEquipCommand(BackpackEquipEmoteCommand command)
        {
            if (!emoteCache.TryGetEmote(command.Id, out IEmote emote))
            {
                ReportHub.LogError(new ReportData(ReportCategory.EMOTE), $"Cannot equip emote, not found: {command.Id}");
                return;
            }

            int slot = command.Slot ?? currentEmoteSlot;

            if (slot is < 0 or >= 10)
            {
                ReportHub.LogError(new ReportData(ReportCategory.EMOTE), $"Cannot equip emote, slot out of bounds: {command.Id} - {command.Slot}");
                return;
            }

            backpackEventBus.SendUnEquipEmote(slot, equippedEmotes.EmoteInSlot(slot));
            backpackEventBus.SendEquipEmote(slot, emote);
        }

        private void HandleUnEquipWearableCommand(BackpackUnEquipWearableCommand command)
        {
            if (!wearableCatalog.TryGetWearable(command.Id, out IWearable? wearable))
            {
                ReportHub.LogError(new ReportData(ReportCategory.WEARABLE), $"Cannot un-equip wearable, not found: {command.Id}");
                return;
            }

            backpackEventBus.SendUnEquipWearable(wearable);
        }

        private void HandleUnEquipEmoteCommand(BackpackUnEquipEmoteCommand command)
        {
            int slot = -1;

            if (command.Slot != null)
                slot = command.Slot.Value;
            else if (command.Id != null)
                slot = equippedEmotes.SlotOf(command.Id);

            if (slot == -1)
            {
                ReportHub.LogError(new ReportData(ReportCategory.EMOTE), $"Cannot unequip emote, slot out of bounds: {command.Id} - {command.Slot}");
                return;
            }

            backpackEventBus.SendUnEquipEmote(slot, equippedEmotes.EmoteInSlot(slot));
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

        private void HandleEmoteSlotSelectCommand(BackpackEmoteSlotSelectCommand command)
        {
            currentEmoteSlot = command.Slot;
            backpackEventBus.SendEmoteSlotSelect(command.Slot);
        }
    }
}
