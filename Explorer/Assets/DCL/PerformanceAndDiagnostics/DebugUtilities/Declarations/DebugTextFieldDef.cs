using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition for the text generic field
    /// </summary>
    public class DebugTextFieldDef : IDebugElementDef
    {
        public readonly ElementBinding<string> Binding;

        public DebugTextFieldDef(ElementBinding<string> binding)
        {
            Binding = binding;
        }
    }
}
