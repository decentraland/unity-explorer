using System;
using DCL.SDKComponents.SceneUI.Utils;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UIDropdownComponent
    {
        public readonly DropdownField DropdownField = new ();
        public TextElement TextElement { get; private set; }
        public bool IsOnValueChangedTriggered;
        public int LastIndexSetByScene;

        internal Action? cachedScheduledAction;

        public void Initialize(string dropdownName)
        {
            DropdownField.name = dropdownName;
            DropdownField.AddToClassList("dcl-dropdown");
            DropdownField.pickingMode = PickingMode.Position;
            TextElement = DropdownField.Q<TextElement>(className: "unity-base-popup-field__text");

            IsOnValueChangedTriggered = false;
            LastIndexSetByScene = int.MinValue; // -1 is used for the case of 'accept Empty value'

            this.RegisterDropdownCallbacks();
        }

        public void Dispose()
        {
            this.UnregisterDropdownCallbacks();
        }

        internal void AnimateDropdownOpacity()
        {
            // Unity instantiates and removes the Dropdown elements panel every time it's toggled, so the element
            // has to be looked up again.
            var root = DropdownField.panel.visualTree;
            var dropdownOuterContainer = root.Q(null, "unity-base-dropdown__container-outer");
            if (dropdownOuterContainer == null)
                return;

            dropdownOuterContainer.experimental.animation
                                  .Start(0f, 1f, 200, Extensions.OPACITY_ANIMATION_CALLBACK);
        }
    }
}
