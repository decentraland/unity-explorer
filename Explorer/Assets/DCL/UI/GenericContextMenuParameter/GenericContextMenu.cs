using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI
{

    [Serializable]
    public struct GenericContextMenuControlConfig
    {
        public Sprite Sprite;
        public string Text;
    }

    /// <summary>
    ///     Main generic context menu class, used to invoke the MVC manager
    /// </summary>
    public class GenericContextMenu
    {
        private static readonly RectOffset DEFAULT_VERTICAL_LAYOUT_PADDING = new (8, 8, 4, 12);
        private static readonly Vector2 DEFAULT_OFFSET_FROM_TARGET = new (11, 18);

        public readonly List<GenericContextMenuElement> contextMenuSettings = new ();
        public readonly float width;
        public readonly RectOffset verticalLayoutPadding;
        public readonly int elementsSpacing;
        public readonly bool showRim;
        public ContextMenuOpenDirection anchorPoint;
        public Vector2 offsetFromTarget;

        /// <summary>
        ///     Main context menu class.
        ///     offsetFromTarget has the default value of (11, 18).
        ///     horizontalLayoutPadding has the default value of (8, 8, 4, 12).
        /// </summary>
        public GenericContextMenu(float width = 186, Vector2? offsetFromTarget = null, RectOffset verticalLayoutPadding = null, int elementsSpacing = 1, ContextMenuOpenDirection anchorPoint = ContextMenuOpenDirection.BOTTOM_RIGHT,
            bool showRim = false)
        {
            this.width = width;
            this.offsetFromTarget = offsetFromTarget ?? DEFAULT_OFFSET_FROM_TARGET;
            this.verticalLayoutPadding = verticalLayoutPadding ?? DEFAULT_VERTICAL_LAYOUT_PADDING;
            this.elementsSpacing = elementsSpacing;
            this.anchorPoint = anchorPoint;
            this.showRim = showRim;
        }

        public GenericContextMenu AddControl(IContextMenuControlSettings settings)
        {
            contextMenuSettings.Add(new GenericContextMenuElement(settings));
            return this;
        }

        public GenericContextMenu AddControl(GenericContextMenuElement element)
        {
            contextMenuSettings.Add(element);
            return this;
        }

        public void ClearControls()
        {
            contextMenuSettings.Clear();
        }

        public void ChangeAnchorPoint(ContextMenuOpenDirection newAnchorPoint)
        {
            this.anchorPoint = newAnchorPoint;
        }

        public void ChangeOffsetFromTarget(Vector2 offsetFromTarget)
        {
            this.offsetFromTarget = offsetFromTarget;
        }
    }

    public class GenericContextMenuElement
    {
        public readonly IContextMenuControlSettings setting;

        public bool Enabled { get; set; }
        public bool Interactable { get; set; }
        public string? NonInteractableFeedback { get; set; }

        public GenericContextMenuElement(IContextMenuControlSettings setting, bool defaultEnabled, bool defaultInteractable = true, string? defaultNonInteractableFeedback = null)
        {
            this.setting = setting;
            this.Enabled = defaultEnabled;
            this.Interactable = defaultInteractable;
            this.NonInteractableFeedback = defaultInteractable ? null : defaultNonInteractableFeedback;
        }

        public GenericContextMenuElement(IContextMenuControlSettings setting)
        {
            this.setting = setting;
            this.Enabled = true;
            this.Interactable = true;
            this.NonInteractableFeedback = null;
        }
    }
}
