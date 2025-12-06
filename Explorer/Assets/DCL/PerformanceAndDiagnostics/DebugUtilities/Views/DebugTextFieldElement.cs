using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugTextFieldElement : DebugElementBase<DebugTextFieldElement, DebugTextFieldDef>
    {
        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<TextField>());
        }
    }
}
