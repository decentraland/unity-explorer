using DCL.DebugUtilities.Declarations;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugConstLabelElement : DebugElementBase<DebugConstLabelElement, DebugConstLabelDef>
    {
        public new class UxmlFactory : UxmlFactory<DebugConstLabelElement> { }

        protected override void ConnectBindings()
        {
            Label label = this.Q<Label>();
            label.text = definition.Text;
        }
    }
}
