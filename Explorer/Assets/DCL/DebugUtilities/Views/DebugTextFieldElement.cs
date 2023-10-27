using DCL.DebugUtilities.Declarations;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugTextFieldElement : DebugElementBase<DebugTextFieldElement, DebugTextFieldDef>
    {
        public new class UxmlFactory : UxmlFactory<DebugTextFieldElement, UxmlTraits> { }

        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<TextField>());
        }
    }
}
