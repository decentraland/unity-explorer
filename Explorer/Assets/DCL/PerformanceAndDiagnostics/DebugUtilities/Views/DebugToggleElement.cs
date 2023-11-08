using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugToggleElement : DebugElementBase<DebugToggleElement, DebugToggleDef>
    {
        protected override void ConnectBindings()
        {
            Toggle toggle = this.Q<Toggle>();
            toggle.SetValueWithoutNotify(definition.InitialState);
            toggle.RegisterValueChangedCallback(definition.OnToggle);
        }

        public new class UxmlFactory : UxmlFactory<DebugToggleElement, UxmlTraits> { }
    }
}
