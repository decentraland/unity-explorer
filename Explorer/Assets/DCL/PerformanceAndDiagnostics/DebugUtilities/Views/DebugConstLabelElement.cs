using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugConstLabelElement : DebugElementBase<DebugConstLabelElement, DebugConstLabelDef>
    {
        protected override void ConnectBindings()
        {
            Label label = this.Q<Label>();
            label.text = definition.Text;
        }
    }
}
