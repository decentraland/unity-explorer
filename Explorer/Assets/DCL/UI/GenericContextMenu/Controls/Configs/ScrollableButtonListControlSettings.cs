using DCL.UI.GenericContextMenuParameter;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class ScrollableButtonListControlSettings : IContextMenuControlSettings
    {
        private static readonly RectOffset DEFAULT_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);

        internal readonly RectOffset verticalLayoutPadding;
        internal readonly int elementsSpacing;
        internal readonly RectOffset horizontalLayoutPadding;
        internal readonly int horizontalLayoutSpacing;
        internal readonly int maxHeight;
        internal ICollection<string> dataLabels;
        internal readonly Action<int> callback;

        public ScrollableButtonListControlSettings(int elementsSpacing, int maxHeight, Action<int> callback,
            RectOffset horizontalLayoutPadding = null, int horizontalLayoutSpacing = 10, RectOffset verticalLayoutPadding = null)
        {
            this.elementsSpacing = elementsSpacing;
            this.maxHeight = maxHeight;
            this.verticalLayoutPadding = verticalLayoutPadding ?? DEFAULT_VERTICAL_LAYOUT_PADDING;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? new RectOffset(8, 8, 0, 0);
            this.horizontalLayoutSpacing = horizontalLayoutSpacing;
            this.callback = callback;
        }

        public void SetData(ICollection<string> data)
        {
            this.dataLabels = data;
        }
    }
}
