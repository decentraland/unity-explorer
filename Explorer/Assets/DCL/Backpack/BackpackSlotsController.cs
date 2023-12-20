using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Backpack
{
    public class BackpackSlotsController
    {
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackEventBus backpackEventBus;
        private readonly Dictionary<string, AvatarSlotView> avatarSlots = new Dictionary<string, AvatarSlotView>();
        private AvatarSlotView previousSlot;

        public BackpackSlotsController(AvatarSlotView[] avatarSlotViews, BackpackCommandBus backpackCommandBus, BackpackEventBus backpackEventBus)
        {
            this.backpackCommandBus = backpackCommandBus;
            this.backpackEventBus = backpackEventBus;

            backpackEventBus.EquipEvent += EquipInSlot;
            backpackEventBus.UnEquipEvent += UnEquipInSlot;

            foreach (var avatarSlotView in avatarSlotViews)
            {
                avatarSlots.Add(avatarSlotView.Category, avatarSlotView);
                avatarSlotView.OnSlotButtonPressed += OnSlotButtonPressed;
                avatarSlotView.UnequipButton.onClick.AddListener(() => backpackCommandBus.SendCommand(new BackpackCommand(BackpackCommandType.UnequipCommand, null, avatarSlotView.Category)));
            }
        }

        private void UnEquipInSlot(IWearable unequippedWearable)
        {

        }

        private void EquipInSlot(IWearable equippedWearable)
        {
            //avatarSlots[equippedWearable.GetCategory()].
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
    }
}
