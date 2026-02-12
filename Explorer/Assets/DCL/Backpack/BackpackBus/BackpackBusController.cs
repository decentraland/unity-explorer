using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using UnityEngine.Pool;
using Utility;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackBusController : IDisposable
    {
        private readonly IWearableStorage wearableStorage;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IBackpackCommandBus backpackCommandBus;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IEmoteStorage emoteStorage;
        private readonly IEquippedWearables equippedWearables;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IEmoteProvider emotesProvider;

        private readonly CancellationTokenSource fetchWearableCts = new ();
        private readonly CancellationTokenSource fetchEmoteCts = new ();
        private CancellationTokenSource equipOutfitCts = new ();

        private int currentEmoteSlot = -1;

        public BackpackBusController(
            IWearableStorage wearableStorage,
            IBackpackEventBus backpackEventBus,
            IBackpackCommandBus backpackCommandBus,
            IEquippedWearables equippedWearables,
            IEquippedEmotes equippedEmotes,
            IEmoteStorage emoteStorage,
            IWearablesProvider wearablesProvider,
            IEmoteProvider emotesProvider)
        {
            this.wearableStorage = wearableStorage;
            this.backpackEventBus = backpackEventBus;
            this.backpackCommandBus = backpackCommandBus;
            this.equippedEmotes = equippedEmotes;
            this.emoteStorage = emoteStorage;
            this.equippedWearables = equippedWearables;
            this.wearablesProvider = wearablesProvider;
            this.emotesProvider = emotesProvider;

            this.backpackCommandBus.EquipWearableMessageReceived += HandleEquipWearableCommand;
            this.backpackCommandBus.UnEquipWearableMessageReceived += HandleUnEquipWearableCommand;
            this.backpackCommandBus.EquipEmoteMessageReceived += HandleEmoteEquipCommand;
            this.backpackCommandBus.UnEquipEmoteMessageReceived += HandleUnEquipEmoteCommand;
            this.backpackCommandBus.HideMessageReceived += HandleHideCommand;
            this.backpackCommandBus.SelectWearableMessageReceived += HandleSelectWearableCommand;
            this.backpackCommandBus.SelectEmoteMessageReceived += HandleSelectEmoteCommand;
            this.backpackCommandBus.FilterMessageReceived += HandleFilterCommand;
            this.backpackCommandBus.PublishProfileReceived += HandlePublishProfile;
            this.backpackCommandBus.ChangeColorMessageReceived += HandleChangeColor;
            this.backpackCommandBus.UnEquipAllMessageReceived += HandleUnequipAll;
            this.backpackCommandBus.UnEquipAllWearablesMessageReceived += HandleUnEquipAllWearables;
            this.backpackCommandBus.EmoteSlotSelectMessageReceived += HandleEmoteSlotSelectCommand;
            this.backpackCommandBus.EquipOutfitMessageReceived += HandleEquipOutfitCommand;
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
            backpackCommandBus.FilterMessageReceived -= HandleFilterCommand;
            backpackCommandBus.PublishProfileReceived -= HandlePublishProfile;
            backpackCommandBus.ChangeColorMessageReceived -= HandleChangeColor;
            backpackCommandBus.UnEquipAllMessageReceived -= HandleUnequipAll;
            backpackCommandBus.UnEquipAllWearablesMessageReceived -= HandleUnEquipAllWearables;
            backpackCommandBus.EmoteSlotSelectMessageReceived -= HandleEmoteSlotSelectCommand;
            backpackCommandBus.EquipOutfitMessageReceived -= HandleEquipOutfitCommand;

            equipOutfitCts.SafeCancelAndDispose();
            fetchWearableCts.SafeCancelAndDispose();
            fetchEmoteCts.SafeCancelAndDispose();
        }

        private void HandleEquipOutfitCommand(BackpackEquipOutfitCommand command)
        {
            equipOutfitCts = equipOutfitCts.SafeRestart();
            EquipOutfitAsync(command, equipOutfitCts.Token).Forget();
        }

        private void HandlePublishProfile(BackpackPublishProfileCommand command) =>
            backpackEventBus.SendPublishProfile();

        private void HandleUnequipAll(BackpackUnEquipAllCommand obj) =>
            backpackEventBus.SendUnEquipAll();

        private void HandleUnEquipAllWearables(BackpackUnEquipAllWearablesCommand obj)
        {
            backpackEventBus.SendUnEquipAllWearables();
        }

        private void HandleChangeColor(BackpackChangeColorCommand command) =>
            backpackEventBus.SendChangeColor(command.NewColor, command.Category);

        private void HandleFilterCommand(BackpackFilterCommand command) =>
            backpackEventBus.SendFilter(command.Category, command.CategoryEnum, command.SearchText);

        private void HandleSelectWearableCommand(BackpackSelectWearableCommand command) =>
            WearableProviderHelper.FetchWearableByPointerAndExecuteAsync(command.Id, wearablesProvider, wearableStorage, equippedWearables, item => SelectWearable(item, command), fetchWearableCts.Token).Forget();

        private void SelectWearable(IWearable wearable, BackpackSelectWearableCommand command)
        {
            backpackEventBus.SendWearableSelect(wearable);
            command.EndAction?.Invoke();
        }

        private void HandleEquipWearableCommand(BackpackEquipWearableCommand command) =>
            WearableProviderHelper.FetchWearableByPointerAndExecuteAsync(command.Id, wearablesProvider, wearableStorage, equippedWearables, item => EquipWearable(item, command), fetchWearableCts.Token).Forget();

        private void EquipWearable(IWearable wearable, BackpackEquipWearableCommand command)
        {
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
                ReportHub.LogError(new ReportData(ReportCategory.WEARABLE), $"Cannot equip wearable, category is invalid: {wearable.GetUrn()}");
                return;
            }

            IWearable? wearableToUnequip = equippedWearables.Wearable(category);

            if (wearableToUnequip != null)
                backpackEventBus.SendUnEquipWearable(wearableToUnequip);

            backpackEventBus.SendEquipWearable(wearable, command.IsManuallyEquipped);

            if (wearable.Type == WearableType.BodyShape)
                UnEquipIncompatibleWearables(wearable);

            command.EndAction?.Invoke();
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

        private void HandleEmoteEquipCommand(BackpackEquipEmoteCommand command) =>
            EmoteProviderHelper.FetchEmoteByPointerAndExecuteAsync(command.Id, emotesProvider, emoteStorage, equippedWearables, emote => EquipEmote(emote, command), fetchEmoteCts.Token).Forget();

        private void EquipEmote(IEmote emote, BackpackEquipEmoteCommand command)
        {
            int slot = command.Slot ?? currentEmoteSlot;

            if (slot is < 0 or >= 10)
            {
                ReportHub.LogError(new ReportData(ReportCategory.EMOTE), $"Cannot equip emote, slot out of bounds: {command.Id} - {command.Slot}");
                return;
            }

            backpackEventBus.SendUnEquipEmote(slot, equippedEmotes.EmoteInSlot(slot));
            backpackEventBus.SendEquipEmote(slot, emote, command.IsManuallyEquipped);
            command.EndAction?.Invoke();
        }

        private void HandleUnEquipWearableCommand(BackpackUnEquipWearableCommand command)
        {
            if (!wearableStorage.TryGetElement(command.Id, out IWearable? wearable))
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

        private void HandleSelectEmoteCommand(BackpackSelectEmoteCommand command) =>
            EmoteProviderHelper.FetchEmoteByPointerAndExecuteAsync(command.Id, emotesProvider, emoteStorage, equippedWearables, emote => SelectEmote(emote, command), fetchEmoteCts.Token).Forget();

        private void SelectEmote(IEmote emote, BackpackSelectEmoteCommand command)
        {
            backpackEventBus.SendEmoteSelect(emote);
            command.EndAction?.Invoke();
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

        private async UniTaskVoid EquipOutfitAsync(BackpackEquipOutfitCommand command, CancellationToken ct)
        {
            try
            {
                var resolvedWearables = new List<IWearable>();
                var missingUrns = new List<URN>();

                void TryAdd(string urn)
                {
                    if (string.IsNullOrEmpty(urn)) return;

                    if (wearableStorage.TryGetElement(urn, out IWearable w))
                    {
                        resolvedWearables.Add(w);
                    }
                    else
                    {
                        try
                        {
                            missingUrns.Add(new URN(urn));
                        }
                        catch (Exception e)
                        {
                            ReportHub.LogError(ReportCategory.BACKPACK, $"Invalid URN in outfit: {urn}. Error: {e.Message}");
                        }
                    }
                }

                TryAdd(command.BodyShape);

                foreach (var w in command.Wearables)
                    TryAdd(w);

                if (missingUrns.Count > 0)
                {
                    BodyShape bodyShape = BodyShape.FromStringSafe(command.BodyShape);

                    var fetched =
                        await wearablesProvider.GetByPointersAsync(missingUrns, bodyShape, ct);

                    if (fetched != null)
                        resolvedWearables.AddRange(fetched);
                }

                // NOTE: simulate outfit loading takes a bit of time to make sure that
                // NOTE: we can cancel previous outfit loading
                // NOTE: await UniTask.Delay(2000, cancellationToken: ct);

                if (!ct.IsCancellationRequested)
                {
                    backpackEventBus.SendEquipOutfit(command, resolvedWearables.ToArray());
                }
            }
            catch (OperationCanceledException)
            {
                // Correctly swallowed: The user switched outfits, so we abort this one.
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.BACKPACK));
            }
        }
    }
}
