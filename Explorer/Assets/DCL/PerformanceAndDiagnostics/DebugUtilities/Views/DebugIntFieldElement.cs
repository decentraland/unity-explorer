using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugIntFieldElement : DebugElementBase<DebugIntFieldElement, DebugIntFieldDef>
    {
        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<IntegerField>());
        }
    }
}
