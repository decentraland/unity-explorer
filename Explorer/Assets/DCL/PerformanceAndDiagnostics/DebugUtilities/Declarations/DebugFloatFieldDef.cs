using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    public class DebugFloatFieldDef : IDebugElementDef
    {
        public readonly IElementBinding<float> Binding;

        public DebugFloatFieldDef(IElementBinding<float> binding)
        {
            Binding = binding;
        }
    }
}
