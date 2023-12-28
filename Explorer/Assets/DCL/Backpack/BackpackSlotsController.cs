using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using System;
using System.Collections.Generic;

namespace DCL.Backpack
{
    public class BackpackSlotsController : IDisposable
    {
        private readonly BackpackEventBus backpackEventBus;
        private readonly Dictionary<string, AvatarSlotView> avatarSlots = new ();
        private AvatarSlotView previousSlot;

        public BackpackSlotsController(AvatarSlotView[] avatarSlotViews, BackpackCommandBus backpackCommandBus, BackpackEventBus backpackEventBus)
        {
            this.backpackEventBus = backpackEventBus;

            this.backpackEventBus.EquipEvent += EquipInSlot;
            this.backpackEventBus.UnEquipEvent += UnEquipInSlot;

            foreach (var avatarSlotView in avatarSlotViews)
            {
                avatarSlots.Add(avatarSlotView.Category, avatarSlotView);
                avatarSlotView.OnSlotButtonPressed += OnSlotButtonPressed;
                avatarSlotView.UnequipButton.onClick.AddListener(() => backpackCommandBus.SendCommand(new BackpackUnEquipCommand(avatarSlotView.SlotWearableUrn)));
            }
        }

        private void UnEquipInSlot(IWearable wearable)
        {
            avatarSlots[wearable.GetCategory()].SlotWearableUrn = null;
        }

        private void EquipInSlot(IWearable equippedWearable)
        {
            avatarSlots[equippedWearable.GetCategory()].SlotWearableUrn = equippedWearable.GetUrn();
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
