using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Input.Crosshair
{
    public class CrosshairCanvas : VisualElement, ICrosshairView
    {
        private VisualElement crossHairElement;
        private Sprite crossHair;
        private Sprite crossHairInteractable;
        private Length leftLength;
        private Length bottomLength;
        private CursorStyle currentState;
        private VisualElement cursorElement;

        public void Initialize(Sprite crosshair, Sprite crosshairInteractable)
        {
            crossHairInteractable = crosshairInteractable;
            crossHair = crosshair;

            crossHairElement = this.Query<VisualElement>("Crosshair").First();
            cursorElement = this.Query<VisualElement>("Cursor").First();

            leftLength = new Length(0, LengthUnit.Percent);
            bottomLength = new Length(0, LengthUnit.Percent);
            SetCursorStyle(CursorStyle.Interaction);
        }

        public void SetPosition(Vector2 newPosition)
        {
            leftLength.value = newPosition.x;
            bottomLength.value = newPosition.y;

            style.left = leftLength;
            style.bottom = bottomLength;
        }

        public void ResetPosition()
        {
            SetPosition(new Vector2(50, 50));
        }

        public void SetCursorStyle(CursorStyle style)
        {
            if (currentState == style)
                return;

            cursorElement.visible = style == CursorStyle.CameraPan;
            crossHairElement.visible = style != CursorStyle.CameraPan;

            Sprite sprite = style switch
                            {
                                CursorStyle.Interaction => crossHairInteractable,
                                _ => crossHair,
                            };

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
