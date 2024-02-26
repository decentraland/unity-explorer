using DCL.DebugUtilities.UIBindings;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition for the toggle
    /// </summary>
    public class DebugToggleDef : IDebugElementDef
    {
        public readonly ElementBinding<bool> Binding;

        public DebugToggleDef(EventCallback<ChangeEvent<bool>> onToggle, bool initialState)
        {
            Binding = new ElementBinding<bool>(initialState, onToggle);
        }

        public DebugToggleDef(ElementBinding<bool> binding)
        {
            Binding = binding;
        }
    }
}
