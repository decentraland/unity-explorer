using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugSetOnlyLabelElement : DebugElementBase<DebugSetOnlyLabelElement, DebugSetOnlyLabelDef>
    {
        protected override void ConnectBindings()
        {
            Label label = this.Q<Label>();
            definition.Binding.Connect(label);
        }

        public new class UxmlFactory : UxmlFactory<DebugSetOnlyLabelElement, UxmlTraits> { }
    }
}
