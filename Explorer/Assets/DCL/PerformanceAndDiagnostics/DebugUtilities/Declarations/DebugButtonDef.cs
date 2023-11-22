using JetBrains.Annotations;
using System;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition for the clickable button
    /// </summary>
    public class DebugButtonDef : IDebugElementDef
    {
        public readonly string Text;
        public readonly Action OnClick;

        public DebugButtonDef([CanBeNull] string text, Action onClick)
        {
            Text = text;
            OnClick = onClick;
        }
    }
}
