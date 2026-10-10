using DCL.DebugUtilities.UIBindings;
using System.Collections.Generic;

namespace DCL.DebugUtilities
{
    public class DebugDropdownDef : IDebugElementDef
    {
        public List<string> Choices => Binding.values;

        public readonly IndexedElementBinding Binding;

        public readonly string Label;

        public DebugDropdownDef(IndexedElementBinding binding, string label)
        {
            Binding = binding;
            Label = label;
        }
    }
}
