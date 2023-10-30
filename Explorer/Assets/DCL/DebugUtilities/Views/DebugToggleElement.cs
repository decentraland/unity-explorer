using DCL.DebugUtilities.Declarations;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugToggleElement : DebugElementBase<DebugToggleElement, DebugToggleDef>
    {
        public new class UxmlFactory : UxmlFactory<DebugToggleElement, UxmlTraits> { }

        protected override void ConnectBindings()
        {
            Toggle toggle = this.Q<Toggle>();
            toggle.SetValueWithoutNotify(definition.InitialState);
            toggle.RegisterValueChangedCallback(definition.OnToggle);
        }
    }
}
