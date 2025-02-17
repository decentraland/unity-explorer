using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    /// <summary>
    ///     Main generic context menu class, used to invoke the MVC manager
    /// </summary>
    public class GenericContextMenu
    {
        internal readonly List<IContextMenuControlSettings> contextMenuSettings = new ();
        internal readonly Vector2 offsetFromTarget;
        internal readonly float width;
        internal readonly RectOffset verticalLayoutPadding;
        internal readonly int elementsSpacing;
        internal readonly GenericContextMenuAnchorPoint anchorPoint;

        /// <summary>
        ///     Main context menu class.
        ///     offsetFromTarget has the default value of (11, 18).
        ///     horizontalLayoutPadding has the default value of (8, 8, 4, 12).
        /// </summary>
        public GenericContextMenu(float width = 186, Vector2? offsetFromTarget = null, RectOffset verticalLayoutPadding = null, int elementsSpacing = 1, GenericContextMenuAnchorPoint anchorPoint = GenericContextMenuAnchorPoint.TOP_LEFT)
        {
            this.width = width;
            this.offsetFromTarget = offsetFromTarget ?? new Vector2(11, 18);
            this.verticalLayoutPadding = verticalLayoutPadding ?? new RectOffset(8, 8, 4, 12);
            this.elementsSpacing = elementsSpacing;
            this.anchorPoint = anchorPoint;
        }

        public GenericContextMenu AddControl(IContextMenuControlSettings settings)
        {
            contextMenuSettings.Add(settings);
            return this;
        }
    }

    public enum GenericContextMenuAnchorPoint
    {
        TOP_LEFT,
        TOP_RIGHT,
        BOTTOM_LEFT,
        BOTTOM_RIGHT,
        CENTER_LEFT,
        CENTER_RIGHT,
    }
}
