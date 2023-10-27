using DCL.DebugUtilities.Declarations;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugSetOnlyLabelElement : DebugElementBase<DebugSetOnlyLabelElement, DebugSetOnlyLabelDef>
    {
        public new class UxmlFactory : UxmlFactory<DebugSetOnlyLabelElement, UxmlTraits> { }

        protected override void ConnectBindings()
        {
            Label label = this.Q<Label>();
            definition.Binding.Connect(label);
        }
    }
}
