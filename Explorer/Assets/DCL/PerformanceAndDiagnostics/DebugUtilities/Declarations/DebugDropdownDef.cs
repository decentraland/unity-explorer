using DCL.DebugUtilities.UIBindings;
using System.Collections.Generic;

namespace DCL.DebugUtilities
{
    public class DebugDropdownDef : IDebugElementDef
    {
        public readonly List<string> Choices;

        /// <summary>
        ///     Ideally it should be an index binding but `DropdownField` implements `INotifyValueChanged`string`
        ///     instead of `INotifyValueChanged`int`
        /// </summary>
        public readonly ElementBinding<string> Binding;

        public readonly string Label;

        public readonly int DefaultIndex;

        public DebugDropdownDef(List<string> choices, ElementBinding<string> binding, string label, int defaultIndex = 0)
        {
            Binding = binding;
            Label = label;
            DefaultIndex = defaultIndex;
            Choices = choices;
        }
    }
}
