using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.UI;
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
        private readonly CacheOutfitWearablesCommand  cacheOutfitWearablesCommand;

        private readonly CancellationTokenSource fetchWearableCts = new ();
        private readonly CancellationTokenSource fetchEmoteCts = new ();
        private CancellationTokenSource equipOutfitCts = new ();
        private CancellationTokenSource? selectionCts;

        private readonly ReportData emoteReportData = new (ReportCategory.EMOTE);
        private readonly ReportData wearableReportData = new (ReportCategory.WEARABLE);

        private CancellationTokenSource? selectEmoteCts;

        private int currentEmoteSlot = -1;

        public BackpackBusController(
            IWearableStorage wearableStorage,
            IBackpackEventBus backpackEventBus,
            IBackpackCommandBus backpackCommandBus,
            IEquippedWearables equippedWearables,
            IEquippedEmotes equippedEmotes,
            IEmoteStorage emoteStorage,
            IWearablesProvider wearablesProvider,
            IEmoteProvider emotesProvider,
            CacheOutfitWearablesCommand cacheOutfitWearablesCommand)
        {
            this.wearableStorage = wearableStorage;
            this.backpackEventBus = backpackEventBus;
            this.backpackCommandBus = backpackCommandBus;
            this.equippedEmotes = equippedEmotes;
            this.emoteStorage = emoteStorage;
            this.equippedWearables = equippedWearables;
            this.wearablesProvider = wearablesProvider;
            this.emotesProvider = emotesProvider;
            this.cacheOutfitWearablesCommand = cacheOutfitWearablesCommand;

            backpackEventBus.ChangedBackpackSectionEvent += OnBackpackSectionChanged;
            backpackEventBus.DeactivateEvent += CancelEmoteSelection;
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
            backpackEventBus.ChangedBackpackSectionEvent -= OnBackpackSectionChanged;
            backpackEventBus.DeactivateEvent -= CancelEmoteSelection;
            CancelEmoteSelection();
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
            SelectWearableAsync(command).Forget();

        private async UniTask SelectWearableAsync(BackpackSelectWearableCommand command)
        {
            using var selection = CancellationTokenSource.CreateLinkedTokenSource(command.CancellationToken, fetchWearableCts.Token);
            selectionCts?.Cancel();
            selectionCts = selection;
            CancellationToken ct = selection.Token;

            try
            {
                if (ct.IsCancellationRequested) return;
                URN pointer = new URN(command.Id).Shorten();
                if (wearableStorage.TryGetElement(pointer, out IWearable cached) && cached.Model.Succeeded)
                {
                    backpackEventBus.SendWearableSelect(cached);
                    return;
                }

                IWearable? body = equippedWearables.Wearable(Runtime.Wearables.WearableCategories.Categories.BODY_SHAPE);
                if (body == null) return;
                var wearables = await wearablesProvider.GetDTOsByPointersAsync(new[] { pointer }, BodyShape.FromStringSafe(body.GetUrn()), ct);
                if (ct.IsCancellationRequested || wearables == null) return;

                await UniTask.SwitchToMainThread(ct);
                foreach (IWearable wearable in wearables)
                    if (wearable.Model.Succeeded && wearable.GetUrn().Shorten().Equals(pointer))
                    {
                        backpackEventBus.SendWearableSelect(wearable);
                        return;
                    }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e) { ReportHub.LogException(e, wearableReportData); }
            finally
            {
                if (selectionCts == selection) selectionCts = null;
            }
        }

        private bool HandleEquipWearableCommand(BackpackEquipWearableCommand command)
        {
            IWearable wearable = command.Wearable;
            string category = wearable.GetCategory();
            if (string.IsNullOrEmpty(category)) return false;

            IWearable? currentBody = equippedWearables.Wearable(Runtime.Wearables.WearableCategories.Categories.BODY_SHAPE);
            if (currentBody == null) return false;

            BodyShape targetBody = WearableComponentsUtils.GetBodyShapeForEquip(wearable.GetUrn(), BodyShape.FromStringSafe(currentBody.GetUrn()));
            if (!wearable.HasUsableMainAsset(targetBody)) return false;

            IWearable? wearableToUnequip = equippedWearables.Wearable(category);

            if (wearableToUnequip != null)
                backpackEventBus.SendUnEquipWearable(wearableToUnequip);

            backpackEventBus.SendEquipWearable(wearable, true);

            if (wearable.Type == WearableType.BodyShape)
                UnEquipIncompatibleWearables(wearable);

            return true;
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
            ElementProviderHelper.FetchElementByPointerAndExecuteAsync(command.Id, emotesProvider, emoteStorage, equippedWearables, emote => EquipEmote(emote, command), fetchEmoteCts.Token, emoteReportData).Forget();

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
            if (!wearableStorage.TryGetElement(command.Id, out IWearable wearable))
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
            selectEmoteCts = selectEmoteCts.SafeRestart();
            SelectEmoteAsync(command, selectEmoteCts.Token).Forget();
        }

        private async UniTaskVoid SelectEmoteAsync(BackpackSelectEmoteCommand command, CancellationToken ct)
        {
            try
            {
                if (emoteStorage.TryGetElement(command.Id, out IEmote cached) && cached.Model.Succeeded)
                {
                    backpackEventBus.SendEmoteSelect(cached);
                    return;
                }

                using var pointerScope = ListPool<URN>.Get(out var pointers);
                using var resultScope = ListPool<IEmote>.Get(out var emotes);
                pointers.Add(command.Id);
                await emotesProvider.GetMetadataByPointersAsync(pointers, ct, emotes);
                ct.ThrowIfCancellationRequested();

                if (emotes.Count == 1)
                    backpackEventBus.SendEmoteSelect(emotes[0]);
                else
                    ReportHub.LogWarning(emoteReportData, $"Couldn't fetch metadata for emote {command.Id}");
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, emoteReportData); }
            finally { command.EndAction?.Invoke(); }
        }

        private void OnBackpackSectionChanged(BackpackSections section)
        {
            if (section != BackpackSections.Emotes)
                CancelEmoteSelection();
        }

        private void CancelEmoteSelection() =>
            selectEmoteCts.SafeCancelAndDispose();

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
            using var _ = ListPool<URN>.Get(out var urns);

            try
            {
                BodyShape bodyShape = BodyShape.FromStringSafe(command.BodyShape);

                foreach (var wearableUrn in command.Wearables)
                    if (wearableUrn != null)
                        urns.Add(wearableUrn);

                List<IWearable> resolvedWearables = new List<IWearable>(urns.Count);

                await cacheOutfitWearablesCommand.ExecuteAsync(urns, bodyShape, ct, resolvedWearables, command.UseFullUrns);

                if (ct.IsCancellationRequested)
                    return;

                backpackEventBus.SendEquipOutfit(command, resolvedWearables);
            }
            catch (OperationCanceledException)
            {
                // Correctly swallowed: The user switched outfits, so we abort this one.
            }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.BACKPACK)); }
            finally
            {
                backpackEventBus.SendEquipOutfitCompleted();
            }
        }
    }
}
