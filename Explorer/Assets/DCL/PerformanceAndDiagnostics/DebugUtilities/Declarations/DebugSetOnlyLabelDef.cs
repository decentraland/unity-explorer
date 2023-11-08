using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition of the dynamic label with a custom value set from code
    /// </summary>
    public class DebugSetOnlyLabelDef : IDebugElementDef
    {
        public readonly ElementBinding<string> Binding;

        public DebugSetOnlyLabelDef(ElementBinding<string> binding)
        {
            Binding = binding;
        }
    }
}
