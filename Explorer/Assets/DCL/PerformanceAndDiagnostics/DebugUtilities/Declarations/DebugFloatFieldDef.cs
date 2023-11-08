using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    public class DebugFloatFieldDef : IDebugElementDef
    {
        public readonly ElementBinding<float> Binding;

        public DebugFloatFieldDef(ElementBinding<float> binding)
        {
            Binding = binding;
        }
    }
}
