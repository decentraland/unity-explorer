using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugIntFieldElement : DebugElementBase<DebugIntFieldElement, DebugIntFieldDef>
    {
        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<IntegerField>());
        }

        public new class UxmlFactory : UxmlFactory<DebugIntFieldElement, UxmlTraits> { }
    }
}
