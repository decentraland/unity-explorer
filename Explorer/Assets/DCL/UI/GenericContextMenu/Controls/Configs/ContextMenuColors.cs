using UnityEngine;

namespace DCL.UI.Controls.Configs
{
    public static class ContextMenuColors
    {
        /// <summary>
        /// #FCFCFC — default text color for context menu buttons.
        /// </summary>
        public static readonly Color DEFAULT_TEXT = new (0.988f, 0.988f, 0.988f, 1f);

        /// <summary>
        /// #FFFFFF — default icon color for context menu buttons.
        /// </summary>
        public static readonly Color DEFAULT_ICON = new (1f, 1f, 1f, 1f);

        /// <summary>
        /// #FF2D55 — used for destructive actions (Block, Report, etc.)
        /// </summary>
        public static readonly Color DESTRUCTIVE_ACTION = new (1f, 0.176f, 0.333f, 1f);
    }
}
