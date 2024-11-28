using DCL.DebugUtilities.UIBindings;
using System;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition for the clickable button
    /// </summary>
    public class DebugButtonDef : IDebugElementDef
    {
        public readonly ElementBinding<string> Text;
        public readonly Action OnClick;

        public DebugButtonDef(string? text, Action onClick)
            : this(new ElementBinding<string>(text!), onClick) { }

        public DebugButtonDef(ElementBinding<string> text, Action onClick)
        {
            Text = text;
            OnClick = onClick;
        }
    }
}
