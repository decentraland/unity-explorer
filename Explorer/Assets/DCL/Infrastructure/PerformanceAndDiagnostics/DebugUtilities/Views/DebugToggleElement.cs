using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugToggleElement : DebugElementBase<DebugToggleElement, DebugToggleDef>
    {
        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<Toggle>());
        }

        public new class UxmlFactory : UxmlFactory<DebugToggleElement, UxmlTraits> { }
    }
}
