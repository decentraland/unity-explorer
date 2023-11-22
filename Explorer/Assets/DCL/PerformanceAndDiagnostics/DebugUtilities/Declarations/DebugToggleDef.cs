using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition for the toggle
    /// </summary>
    public class DebugToggleDef : IDebugElementDef
    {
        public readonly bool InitialState;
        public readonly EventCallback<ChangeEvent<bool>> OnToggle;

        public DebugToggleDef(EventCallback<ChangeEvent<bool>> onToggle, bool initialState)
        {
            OnToggle = onToggle;
            InitialState = initialState;
        }
    }
}
