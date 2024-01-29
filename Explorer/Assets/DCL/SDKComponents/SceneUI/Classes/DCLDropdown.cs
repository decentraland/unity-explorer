using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Classes
{
    public class DCLDropdown
    {
        public readonly DropdownField DropdownField = new ();
        public TextElement TextElement;

        public void Initialize(string dropdownName, string dropdownStyleClass, string textElementStyleClass)
        {
            DropdownField.name = dropdownName;
            DropdownField.AddToClassList(dropdownStyleClass);
            DropdownField.pickingMode = PickingMode.Position;
            TextElement = DropdownField.Q<TextElement>(className: textElementStyleClass);
        }

        public void Dispose() { }
    }
}
