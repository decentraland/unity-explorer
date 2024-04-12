using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugDropdownElement : DebugElementBase<DebugDropdownElement, DebugDropdownDef>
    {
        protected override void ConnectBindings()
        {
            DropdownField dropdown = this.Q<DropdownField>();
            dropdown.choices = definition.Choices;
            dropdown.label = definition.Label;

            definition.Binding.Connect(this.Q<DropdownField>());
        }

        public new class UxmlFactory : UxmlFactory<DebugDropdownElement, UxmlTraits> { }
    }
}
