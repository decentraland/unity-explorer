using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugSetOnlyLabelElement : DebugElementBase<DebugSetOnlyLabelElement, DebugSetOnlyLabelDef>
    {
        protected override void ConnectBindings()
        {
            Label label = this.Q<Label>();
            definition.Binding.Connect(label);
        }
    }
}
