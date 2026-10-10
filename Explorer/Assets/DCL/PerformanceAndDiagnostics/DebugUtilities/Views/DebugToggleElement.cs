using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugToggleElement : DebugElementBase<DebugToggleElement, DebugToggleDef>
    {
        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<Toggle>());
        }
    }
}
