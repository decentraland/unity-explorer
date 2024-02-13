using DCL.SDKComponents.SceneUI.Utils;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UIDropdownComponent
    {
        public readonly DropdownField DropdownField = new ();
        public TextElement TextElement;
        public bool IsOnValueChangedTriggered;

        internal EventCallback<ChangeEvent<string>> currentOnValueChanged;

        public void Initialize(string dropdownName, string dropdownStyleClass, string textElementStyleClass)
        {
            DropdownField.name = dropdownName;
            DropdownField.AddToClassList(dropdownStyleClass);
            DropdownField.pickingMode = PickingMode.Position;
            TextElement = DropdownField.Q<TextElement>(className: textElementStyleClass);

            IsOnValueChangedTriggered = false;
            this.RegisterDropdownCallbacks();
        }

        public void Dispose()
        {
            this.UnregisterDropdownCallbacks();
        }
    }
}
