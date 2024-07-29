using DCL.ECSComponents;
using System.Collections.Generic;

namespace DCL.Interaction.PlayerOriginated.Components
{
    /// <summary>
    ///     Holds the state to show/hide the hover canvas
    /// </summary>
    public readonly struct HoverFeedbackComponent
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
        public bool Enabled => tooltips.Count > 0;

        public IReadOnlyList<Tooltip> Tooltips => tooltips;

        /// <summary>
        ///     Pre-allocated array with a maximum size for tooltips
        /// </summary>
        private readonly List<Tooltip> tooltips;

        public void Add(in Tooltip tooltip)
        {
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

        public HoverFeedbackComponent(int tooltipsCapacity) : this()
        {
            // tolerate the allocation as this components exists in a single instance in the global world
            tooltips = new List<Tooltip>(tooltipsCapacity);
        }
    }
}
