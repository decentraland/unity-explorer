using DCL.ECSComponents;
using System.Collections.Generic;

namespace DCL.Interaction.PlayerOriginated.Components
{
    /// <summary>
    ///     Holds the state to show/hide the hover canvas
    /// </summary>
    public struct HoverFeedbackComponent
    {
        public readonly struct Tooltip
        {
            public readonly string Text;
            public readonly InputAction Action;

            public Tooltip(string text, InputAction action)
            {
                Text = text;
                Action = action;
            }
        }

        /// <summary>
        ///     Whether feedback should be shown
        /// </summary>
        public bool Enabled => Tooltips.Count > 0;

        /// <summary>
        ///     Pre-allocated array with a maximum size for tooltips
        /// </summary>
        public readonly List<Tooltip> Tooltips;

        private int activeTooltipCount;

        public HoverFeedbackComponent(int tooltipsCapacity) : this()
        {
            // tolerate the allocation as this components exists in a single instance in the global world
            Tooltips = new List<Tooltip>(tooltipsCapacity);
        }
    }
}
