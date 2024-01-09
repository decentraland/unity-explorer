using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using Microsoft.ClearScript.Util.Web;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    public class BackpackSlotsController : IDisposable
    {
        private readonly BackpackEventBus backpackEventBus;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly Dictionary<string, AvatarSlotView> avatarSlots = new ();
        private AvatarSlotView previousSlot;

        public BackpackSlotsController(AvatarSlotView[] avatarSlotViews, BackpackCommandBus backpackCommandBus, BackpackEventBus backpackEventBus, NftTypeIconSO rarityBackgrounds)
        {
            this.backpackEventBus = backpackEventBus;
            this.rarityBackgrounds = rarityBackgrounds;

            this.backpackEventBus.EquipEvent += EquipInSlot;
            this.backpackEventBus.UnEquipEvent += UnEquipInSlot;

            foreach (var avatarSlotView in avatarSlotViews)
            {
                avatarSlots.Add(avatarSlotView.Category.ToLower(), avatarSlotView);
                avatarSlotView.OnSlotButtonPressed += OnSlotButtonPressed;
                avatarSlotView.UnequipButton.onClick.AddListener(() => backpackCommandBus.SendCommand(new BackpackUnEquipCommand(avatarSlotView.SlotWearableUrn)));
            }
        }

        private void UnEquipInSlot(IWearable wearable)
        {
            if (!avatarSlots.TryGetValue(wearable.GetCategory(), out AvatarSlotView avatarSlotView)) return;

            avatarSlotView.UnequipButton.gameObject.SetActive(false);
            avatarSlotView.SlotWearableUrn = null;
            avatarSlotView.SlotWearableThumbnail.gameObject.SetActive(false);
            avatarSlotView.SlotWearableThumbnail.sprite = null;
            avatarSlotView.SlotWearableRarityBackground.sprite = null;
        }

        private void EquipInSlot(IWearable equippedWearable)
        {
            if (!avatarSlots.TryGetValue(equippedWearable.GetCategory(), out AvatarSlotView avatarSlotView))
                return;

            avatarSlotView.SlotWearableUrn = equippedWearable.GetUrn();
            avatarSlotView.SlotWearableRarityBackground.sprite = rarityBackgrounds.GetTypeImage(equippedWearable.GetRarity());
            WaitForThumbnailAsync(equippedWearable, avatarSlotView).Forget();
        }

        private async UniTaskVoid WaitForThumbnailAsync(IWearable equippedWearable, AvatarSlotView avatarSlotView)
        {
            avatarSlotView.LoadingView.StartLoadingAnimation(avatarSlotView.NftContainer);
            do
            {
                await UniTask.Delay(500);
            }
            while (equippedWearable.WearableThumbnail == null);

            avatarSlots[equippedWearable.GetCategory()].SlotWearableThumbnail.sprite = equippedWearable.WearableThumbnail.Value.Asset;
            avatarSlots[equippedWearable.GetCategory()].SlotWearableThumbnail.gameObject.SetActive(true);
            avatarSlotView.LoadingView.FinishLoadingAnimation(avatarSlotView.NftContainer);
        }

        private void OnSlotButtonPressed(AvatarSlotView avatarSlot)
        {
            if (previousSlot != null)
                previousSlot.SelectedBackground.SetActive(false);

            if (avatarSlot == previousSlot)
            {
                previousSlot.SelectedBackground.SetActive(false);
                previousSlot = null;
                return;
            }

            previousSlot = avatarSlot;
            avatarSlot.SelectedBackground.SetActive(true);
        }

        public void Dispose()
        {
            backpackEventBus.EquipEvent -= EquipInSlot;
            backpackEventBus.UnEquipEvent -= UnEquipInSlot;
            foreach (var avatarSlotView in avatarSlots.Values)
            {
                avatarSlotView.OnSlotButtonPressed -= OnSlotButtonPressed;
            }
        }
    }
}
