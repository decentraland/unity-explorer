using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackpackSlotsController
{
    private readonly Dictionary<string, AvatarSlotView> avatarSlots = new Dictionary<string, AvatarSlotView>();
    private AvatarSlotView previousSlot;

    public BackpackSlotsController(AvatarSlotView[] avatarSlotViews)
    {
        foreach (var avatarSlotView in avatarSlotViews)
        {
            avatarSlots.Add(avatarSlotView.Category, avatarSlotView);
            avatarSlotView.OnSlotButtonPressed += OnSlotButtonPressed;
        }
    }

    private void OnSlotButtonPressed(AvatarSlotView avatarSlot)
    {
        if(previousSlot != null)
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
