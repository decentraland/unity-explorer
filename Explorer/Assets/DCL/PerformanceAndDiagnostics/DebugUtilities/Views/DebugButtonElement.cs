using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugButtonElement : DebugElementBase<DebugButtonElement, DebugButtonDef>
    {
        protected override void ConnectBindings()
        {
            Button button = this.Q<Button>();
            button.text = definition.Text;
            button.clicked += definition.OnClick;
        }

        public new class UxmlFactory : UxmlFactory<DebugButtonElement> { }
    }
}
