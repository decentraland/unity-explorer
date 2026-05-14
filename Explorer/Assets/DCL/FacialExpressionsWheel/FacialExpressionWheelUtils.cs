using System;
using UnityEngine;

namespace DCL.FacialExpressionsWheel
{
    public static class FacialExpressionWheelUtils
    {
        public static string GetSlotLabel(int slotIndex) =>
            slotIndex < 9 ? (slotIndex + 1).ToString() : "0";

        public static string GetSlotActionName(int slotIndex) =>
            $"Slot {slotIndex}";

        // "Slot 1" maps to idx 0 ... "Slot 9" maps to idx 8, "Slot 0" maps to idx 9.
        public static int SlotIndexFromActionName(string actionName)
        {
            int n = actionName[^1] - '0';
            return n == 0 ? 9 : n - 1;
        }

        public static void Setup(
            this FacialExpressionWheelSlotView slot,
            int slotIndex,
            Sprite icon,
            Action<int> onPlay,
            Action<int> onHover,
            Action<int> onFocusLeave)
        {
            slot.Slot = slotIndex;
            slot.Icon.sprite = icon;
            slot.SlotLabel.text = GetSlotLabel(slotIndex);
            slot.OnPlay += onPlay;
            slot.OnHover += onHover;
            slot.OnFocusLeave += onFocusLeave;
        }
    }
}