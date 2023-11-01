using DCL.DebugUtilities.Declarations;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugFloatFieldElement : DebugElementBase<DebugFloatFieldElement, DebugFloatFieldDef>
    {
        public new class UxmlFactory : UxmlFactory<DebugFloatFieldElement, UxmlTraits> { }

        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<FloatField>());
        }
    }
}
