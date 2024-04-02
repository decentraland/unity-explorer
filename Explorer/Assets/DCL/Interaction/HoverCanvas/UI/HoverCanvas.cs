using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Interaction.HoverCanvas.UI
{
    public class HoverCanvas : VisualElement, IComparer<HoverCanvasTooltipElement>
    {
        private List<HoverCanvasTooltipElement> tooltips;

        private bool initialized;
        public int TooltipsCount => tooltips.Count;

        public void Initialize()
        {
            if (initialized) return;

            tooltips = this.Query<HoverCanvasTooltipElement>().ToList();
            tooltips.Sort(this);
            initialized = true;
        }

        public void SetPosition(Vector2Int newPosition)
        {
            style.top = -newPosition.y;
            style.left = newPosition.x;
        }

        public void SetTooltip([CanBeNull] string hintText, [CanBeNull] string actionKeyText, [CanBeNull] Sprite icon, int index)
        {
            if (index >= tooltips.Count)
                return;

            tooltips[index].SetData(hintText, actionKeyText, icon);
        }

        public void SetTooltipsCount(int count)
        {
            for (var i = 0; i < tooltips.Count; i++)
                tooltips[i].style.opacity = i < count ? 1 : 0;
        }

        public int Compare(HoverCanvasTooltipElement x, HoverCanvasTooltipElement y) =>
            x.parent.tabIndex.CompareTo(y.parent.tabIndex);

        public new class UxmlFactory : UxmlFactory<HoverCanvas> { }
    }
}
