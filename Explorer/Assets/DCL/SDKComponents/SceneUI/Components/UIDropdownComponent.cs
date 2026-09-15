using DCL.SDKComponents.SceneUI.Utils;
using ECS.StreamableLoading.Fonts;
using System;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UIDropdownComponent
    {
        public readonly DropdownField DropdownField = new ();
        public TextElement TextElement { get; private set; }
        public bool IsOnValueChangedTriggered;
        public int LastIndexSetByScene;

        public SceneFontRequest FontRequest;

        public FontAsset? CustomFont;

        internal Action? cachedScheduledAction;

        public void Initialize(string dropdownName)
        {
            DropdownField.name = dropdownName;
            DropdownField.AddToClassList("dcl-dropdown");
            DropdownField.pickingMode = PickingMode.Position;
            TextElement = DropdownField.Q<TextElement>(className: "unity-base-popup-field__text");

            IsOnValueChangedTriggered = false;
            LastIndexSetByScene = int.MinValue; // -1 is used for the case of 'accept Empty value'
            FontRequest = default(SceneFontRequest);
            CustomFont = null;

            this.RegisterDropdownCallbacks();
        }

        public void Dispose()
        {
            this.UnregisterDropdownCallbacks();
            DropdownField.UnregisterHoverStyleCallbacks();
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
