using DCL.DebugUtilities.UIBindings;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition for the toggle
    /// </summary>
    public class DebugToggleDef : IDebugElementDef
    {
        public readonly IElementBinding<bool> Binding;

        public DebugToggleDef(EventCallback<ChangeEvent<bool>> onToggle, bool initialState)
        {
            Binding = new ElementBinding<bool>(initialState, onToggle);
        }

        public DebugToggleDef(IElementBinding<bool> binding)
        {
            Binding = binding;
        }
    }
}
