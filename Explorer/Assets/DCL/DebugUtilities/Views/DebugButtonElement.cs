using DCL.DebugUtilities.Declarations;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugButtonElement : DebugElementBase<DebugButtonElement, DebugButtonDef>
    {
        public new class UxmlFactory : UxmlFactory<DebugButtonElement> { }

        protected override void ConnectBindings()
        {
            Button button = this.Q<Button>();
            button.text = definition.Text;
            button.clicked += definition.OnClick;
        }
    }
}
