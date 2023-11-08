using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugFloatFieldElement : DebugElementBase<DebugFloatFieldElement, DebugFloatFieldDef>
    {
        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<FloatField>());
        }

        public new class UxmlFactory : UxmlFactory<DebugFloatFieldElement, UxmlTraits> { }
    }
}
