using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugDropdownElement : DebugElementBase<DebugDropdownElement, DebugDropdownDef>
    {
        protected override void ConnectBindings()
        {
            DropdownField dropdown = this.Q<DropdownField>();
            dropdown.choices = definition.Choices;
            dropdown.label = definition.Label;

            definition.Binding.Connect(this.Q<DropdownField>());
        }
    }
}
