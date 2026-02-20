using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugButtonElement : DebugElementBase<DebugButtonElement, DebugButtonDef>
    {
        protected override void ConnectBindings()
        {
            Button button = this.Q<Button>();
            definition.Text.Connect(button);
            button.clicked += definition.OnClick;
        }
    }
}
