using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugConstLabelElement : DebugElementBase<DebugConstLabelElement, DebugConstLabelDef>
    {
        protected override void ConnectBindings()
        {
            Label label = this.Q<Label>();
            label.text = definition.Text;
        }

        public new class UxmlFactory : UxmlFactory<DebugConstLabelElement> { }
    }
}
