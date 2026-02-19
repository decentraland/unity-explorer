using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Interaction.HoverCanvas.UI
{
    public class HoverCanvas : VisualElement
    {
        private static readonly int[] LAYOUT_1_PROXIMITY = { 5 };
        private static readonly int[] LAYOUT_1_CURSOR = { 6 };
        private static readonly int[] LAYOUT_2 = { 4, 6 };
        private static readonly int[] LAYOUT_3 = { 2, 4, 6 };
        private static readonly int[] LAYOUT_4 = { 2, 4, 6, 8 };
        private static readonly int[] LAYOUT_5 = { 2, 4, 7, 6, 9 };
        private static readonly int[] LAYOUT_6 = { 1, 4, 6, 3, 6, 9 };
        private static readonly int[] LAYOUT_7 = { 0, 1, 4, 7, 3, 6, 9 };

        private static readonly int[][] CURSOR_LAYOUTS =
        {
            LAYOUT_1_CURSOR,
            LAYOUT_2,
            LAYOUT_3,
            LAYOUT_4,
            LAYOUT_5,
            LAYOUT_6,
            LAYOUT_7,
        };

        private static readonly int[][] PROXIMITY_LAYOUTS =
        {
            LAYOUT_1_PROXIMITY,
            LAYOUT_2,
            LAYOUT_3,
            LAYOUT_4,
            LAYOUT_5,
            LAYOUT_6,
            LAYOUT_7,
        };

        private List<HoverCanvasTooltipElement> tooltips;

        private bool initialized;
        private Length leftLength;
        private Length bottomLength;
        private int[] selectedLayoutIndices = {};
        private int lastLayoutCount = -1;
        private bool lastLayoutIsProximity;

        public int TooltipsCount => tooltips.Count;

        public void Initialize()
        {
            if (initialized) return;

            tooltips = this.Query<HoverCanvasTooltipElement>().ToList();
            initialized = true;

            leftLength = new Length(0, LengthUnit.Percent);
            bottomLength = new Length(0, LengthUnit.Percent);
            pickingMode = PickingMode.Ignore;
        }

        public void SetPosition(Vector2 newPosition)
        {
            leftLength.value = newPosition.x;
            bottomLength.value = newPosition.y;

            style.left = leftLength;
            style.bottom = bottomLength;
        }

        public void UpdateLayout(int count, bool isProximity)
        {
            if (count == lastLayoutCount && isProximity == lastLayoutIsProximity)
                return;

            lastLayoutCount = count;
            lastLayoutIsProximity = isProximity;

            int[][] layouts = isProximity ? PROXIMITY_LAYOUTS : CURSOR_LAYOUTS;
            selectedLayoutIndices = layouts[count - 1];

            ResetAllTooltips();

            for (int i = 0; i < selectedLayoutIndices.Length; i++)
            {
                int tooltipIndex = selectedLayoutIndices[i];
                var tooltipElement = tooltips[tooltipIndex];
                tooltipElement.style.opacity = 1;
                tooltipElement.tabIndex = i;
            }

            Debug.Log($"(Maurizio) count: {count}, isProximity: {isProximity}");
        }

        public void SetTooltip(string? hintText, string? actionKeyText, string? iconClass, int index)
        {
            if (index >= selectedLayoutIndices.Length)
                return;

            int tooltipsIndex = selectedLayoutIndices[index];
            tooltips[tooltipsIndex].SetData(hintText, actionKeyText, iconClass);
        }

        public new class UxmlFactory : UxmlFactory<HoverCanvas> { }

        private void ResetAllTooltips()
        {
            for (int i = 0; i < tooltips.Count; i++)
            {
                tooltips[i].style.opacity = 0;
                tooltips[i].tabIndex = 0;
            }
        }
    }
}
