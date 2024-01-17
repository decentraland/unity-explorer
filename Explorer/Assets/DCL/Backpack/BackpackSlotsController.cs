using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using Microsoft.ClearScript.Util.Web;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    public class BackpackSlotsController : IDisposable
    {
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackEventBus backpackEventBus;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly Dictionary<string, (AvatarSlotView, CancellationTokenSource)> avatarSlots = new ();
        private AvatarSlotView previousSlot;

        public BackpackSlotsController(
            AvatarSlotView[] avatarSlotViews,
            BackpackCommandBus backpackCommandBus,
            BackpackEventBus backpackEventBus,
            NftTypeIconSO rarityBackgrounds)
        {
            this.backpackCommandBus = backpackCommandBus;
            this.backpackEventBus = backpackEventBus;
            this.rarityBackgrounds = rarityBackgrounds;

            this.backpackEventBus.EquipEvent += EquipInSlot;
            this.backpackEventBus.UnEquipEvent += UnEquipInSlot;
            this.backpackEventBus.FilterCategoryEvent += DeselectCategory;

            foreach (var avatarSlotView in avatarSlotViews)
            {
                avatarSlots.Add(avatarSlotView.Category.ToLower(), (avatarSlotView, new CancellationTokenSource()));
                avatarSlotView.OnSlotButtonPressed += OnSlotButtonPressed;
                avatarSlotView.UnequipButton.onClick.AddListener(() => backpackCommandBus.SendCommand(new BackpackUnEquipCommand(avatarSlotView.SlotWearableUrn)));
            }
        }

        private void DeselectCategory(string filterContent)
        {
            if (previousSlot != null && string.IsNullOrEmpty(filterContent))
            {
                previousSlot.SelectedBackground.SetActive(false);
                previousSlot = null;
            }
        }

        private void UnEquipInSlot(IWearable wearable)
        {
            if (!avatarSlots.TryGetValue(wearable.GetCategory(), out (AvatarSlotView, CancellationTokenSource) avatarSlotView)) return;

            avatarSlotView.Item2.SafeCancelAndDispose();
            avatarSlotView.Item1.UnequipButton.gameObject.SetActive(false);
            avatarSlotView.Item1.SlotWearableUrn = null;
            avatarSlotView.Item1.SlotWearableThumbnail.gameObject.SetActive(false);
            avatarSlotView.Item1.SlotWearableThumbnail.sprite = null;
            avatarSlotView.Item1.SlotWearableRarityBackground.sprite = null;
            avatarSlotView.Item1.EmptyOverlay.SetActive(true);
        }

        private void EquipInSlot(IWearable equippedWearable)
        {
            if (!avatarSlots.TryGetValue(equippedWearable.GetCategory(), out (AvatarSlotView, CancellationTokenSource) avatarSlotView))
                return;

            avatarSlotView.Item1.SlotWearableUrn = equippedWearable.GetUrn();
            avatarSlotView.Item1.SlotWearableRarityBackground.sprite = rarityBackgrounds.GetTypeImage(equippedWearable.GetRarity());
            avatarSlotView.Item1.EmptyOverlay.SetActive(false);

            avatarSlotView.Item2.SafeCancelAndDispose();
            avatarSlotView.Item2 = new CancellationTokenSource();
            WaitForThumbnailAsync(equippedWearable, avatarSlotView.Item1, avatarSlotView.Item2.Token).Forget();
        }

        private async UniTaskVoid WaitForThumbnailAsync(IWearable equippedWearable, AvatarSlotView avatarSlotView, CancellationToken ct)
        {
            avatarSlotView.LoadingView.StartLoadingAnimation(avatarSlotView.NftContainer);
            do
            {
                await UniTask.Delay(500, cancellationToken: ct);
            }
            while (equippedWearable.WearableThumbnail == null);

            avatarSlots[equippedWearable.GetCategory()].Item1.SlotWearableThumbnail.sprite = equippedWearable.WearableThumbnail.Value.Asset;
            avatarSlots[equippedWearable.GetCategory()].Item1.SlotWearableThumbnail.gameObject.SetActive(true);
            avatarSlotView.LoadingView.FinishLoadingAnimation(avatarSlotView.NftContainer);
        }

        private void OnSlotButtonPressed(AvatarSlotView avatarSlot)
        {
            if (previousSlot != null)
                previousSlot.SelectedBackground.SetActive(false);

            if (avatarSlot == previousSlot)
            {
                previousSlot.SelectedBackground.SetActive(false);
                backpackCommandBus.SendCommand(new BackpackFilterCategoryCommand(""));
                previousSlot = null;
                return;
            }

            previousSlot = avatarSlot;
            backpackCommandBus.SendCommand(new BackpackFilterCategoryCommand(avatarSlot.Category));
            avatarSlot.SelectedBackground.SetActive(true);
        }

        public void Dispose()
        {
            backpackEventBus.EquipEvent -= EquipInSlot;
            backpackEventBus.UnEquipEvent -= UnEquipInSlot;
            foreach (var avatarSlotView in avatarSlots.Values)
            {
                avatarSlotView.Item1.OnSlotButtonPressed -= OnSlotButtonPressed;
            }
        }
    }
}
