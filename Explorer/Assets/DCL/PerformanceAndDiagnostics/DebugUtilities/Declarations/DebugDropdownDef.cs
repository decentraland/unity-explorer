using DCL.DebugUtilities.UIBindings;
using System.Collections.Generic;

namespace DCL.DebugUtilities
{
    public class DebugDropdownDef : IDebugElementDef
    {
        public readonly List<string> Choices;

        public readonly IndexedElementBinding Binding;

        public readonly string Label;

        public DebugDropdownDef(List<string> choices, IndexedElementBinding binding, string label)
        {
            Binding = binding;
            Label = label;
            Choices = choices;
        }
    }
}
