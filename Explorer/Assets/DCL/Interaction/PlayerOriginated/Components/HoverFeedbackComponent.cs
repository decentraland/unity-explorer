using System.Collections.Generic;
using UnityEngine;

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
            public readonly UnityEngine.InputSystem.InputAction Action;

            public Tooltip(string text, UnityEngine.InputSystem.InputAction action)
            {
                Text = text;
                Action = action;
            }
        }

        /// <summary>
        ///     Whether feedback should be shown
        /// </summary>
        public bool Enabled => tooltips.Count > 0;

        /// <summary>
        ///     Overrides the screen position at which the hover feedback is shown
        /// </summary>
        public Vector2? ScreenPositionOverride { get; set; }

        public IReadOnlyList<Tooltip> Tooltips => tooltips;

        /// <summary>
        ///     Pre-allocated array with a maximum size for tooltips
        /// </summary>
        private readonly List<Tooltip> tooltips;

        private readonly int maxTooltips;

        /// <summary>
        ///     Tooltips beyond the maximum are dropped as the hover canvas has no layout to show them
        /// </summary>
        public void Add(in Tooltip tooltip)
        {
            if (tooltips.Count >= maxTooltips)
                return;

            tooltips.Add(tooltip);
        }

        public void Remove(in Tooltip tooltip)
        {
            tooltips.Remove(tooltip);
        }

        public void Clear()
        {
            tooltips.Clear();
        }

        public HoverFeedbackComponent(int maxTooltips) : this()
        {
            this.maxTooltips = maxTooltips;

            // tolerate the allocation as this components exists in a single instance in the global world
            tooltips = new List<Tooltip>(maxTooltips);
        }
    }
}
