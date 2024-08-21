using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Interaction.HoverCanvas.UI
{
    public class HoverCanvasTooltipElement : VisualElement
    {
        private Label hint;

        private bool initialized;

        private Image inputIcon;
        private Label keyName;
        private VisualElement keyRoot;

        private void Initialize()
        {
            if (initialized)
                return;

            inputIcon = this.Q<Image>("Icon");
            keyName = this.Q<Label>("KeyName");
            hint = this.Q<Label>("Hint");
            keyRoot = this.Q<VisualElement>("KeyRoot");

            initialized = true;
        }

        public void SetData(string? hintText, string? actionKeyText, Sprite? icon)
        {
            Initialize();

            if (!string.IsNullOrEmpty(hintText))
            {
                hint.text = hintText;
                hint.SetDisplayed(true);
            }
            else hint.SetDisplayed(false);

            if (!string.IsNullOrEmpty(actionKeyText))
            {
                keyName.text = actionKeyText;
                keyRoot.SetDisplayed(true);
            }
            else keyRoot.SetDisplayed(false);

            if (icon != null)
            {
                inputIcon.sprite = icon;
                inputIcon.SetDisplayed(true);
            }
            else inputIcon.SetDisplayed(false);
        }

        public new class UxmlFactory : UxmlFactory<HoverCanvasTooltipElement> { }
    }
}
