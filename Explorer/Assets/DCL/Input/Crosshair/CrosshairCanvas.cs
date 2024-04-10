using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Input.Crosshair
{
    public class CrosshairCanvas : VisualElement, ICrosshairView
    {
        private bool initialized;
        private VisualElement crossHairElement;
        private Sprite crossHair;
        private Sprite crossHairInteractable;
        private CursorStyle currentState;

        public void Initialize(Sprite crossHair, Sprite crossHairInteractable)
        {
            this.crossHairInteractable = crossHairInteractable;
            this.crossHair = crossHair;
            if (initialized) return;

            crossHairElement = this.Query<VisualElement>("Crosshair").First();

            initialized = true;
            SetCursorStyle(CursorStyle.Interaction);
        }

        public void SetCursorStyle(CursorStyle style)
        {
            if (currentState == style) return;
            Sprite sprite = style == CursorStyle.Interaction ? crossHairInteractable : crossHair;

            StyleBackground styleBackground = crossHairElement.style.backgroundImage;
            Background background = styleBackground.value;
            background.sprite = sprite;
            styleBackground.value = background;
            crossHairElement.style.backgroundImage = styleBackground;
            currentState = style;
        }

        public void SetDisplayed(bool displayed)
        {
            VisualElementsExtensions.SetDisplayed(this, displayed);
        }

        public new class UxmlFactory : UxmlFactory<CrosshairCanvas> { }
    }
}
