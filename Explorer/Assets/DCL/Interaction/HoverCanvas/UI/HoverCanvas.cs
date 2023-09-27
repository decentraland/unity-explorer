using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Interaction.HoverCanvas.UI
{
    public class HoverCanvas : VisualElement, IComparer<HoverCanvasTooltipElement>
    {
        private List<HoverCanvasTooltipElement> tooltips;

        private bool initialized;

        private void Initialize()
        {
            if (initialized) return;

            tooltips = this.Query<HoverCanvasTooltipElement>().ToList();
            tooltips.Sort(this);

            initialized = true;
        }

        public void SetTooltip([CanBeNull] string hintText, [CanBeNull] string actionKeyText, [CanBeNull] Sprite icon, int index)
        {
            Initialize();

            if (index >= tooltips.Count)
                return;

            tooltips[index].SetData(hintText, actionKeyText, icon);
        }

        public void SetTooltipsCount(int count)
        {
            for (var i = 0; i < tooltips.Count; i++)
                tooltips[i].SetDisplayed(i < count);
        }

        public new class UxmlFactory : UxmlFactory<HoverCanvas> { }

        public int Compare(HoverCanvasTooltipElement x, HoverCanvasTooltipElement y) =>
            x.tabIndex.CompareTo(y.tabIndex);
    }
}
