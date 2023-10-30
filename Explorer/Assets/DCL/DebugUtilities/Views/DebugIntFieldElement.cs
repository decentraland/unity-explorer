using DCL.DebugUtilities.Declarations;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugIntFieldElement : DebugElementBase<DebugIntFieldElement, DebugIntFieldDef>
    {
        public new class UxmlFactory : UxmlFactory<DebugIntFieldElement, UxmlTraits> { }

        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<IntegerField>());
        }
    }
}
