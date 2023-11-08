using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugTextFieldElement : DebugElementBase<DebugTextFieldElement, DebugTextFieldDef>
    {
        protected override void ConnectBindings()
        {
            definition.Binding.Connect(this.Q<TextField>());
        }

        public new class UxmlFactory : UxmlFactory<DebugTextFieldElement, UxmlTraits> { }
    }
}
