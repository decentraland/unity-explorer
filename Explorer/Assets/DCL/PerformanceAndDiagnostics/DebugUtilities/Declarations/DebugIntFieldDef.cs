using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    public class DebugIntFieldDef : IDebugElementDef
    {
        public readonly ElementBinding<int> Binding;

        public DebugIntFieldDef(ElementBinding<int> binding)
        {
            Binding = binding;
        }
    }
}
