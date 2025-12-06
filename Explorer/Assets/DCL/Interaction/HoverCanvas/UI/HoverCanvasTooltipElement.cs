using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Interaction.HoverCanvas.UI
{
    public class HoverCanvasTooltipElement : VisualElement
    {
        private Label hint;

        private bool initialized;

        private VisualElement inputIcon;
        private Label keyName;
        private VisualElement keyRoot;

        private void Initialize()
        {
            if (initialized)
                return;

            inputIcon = this.Q<VisualElement>("Icon");
            keyName = this.Q<Label>("KeyName");
            hint = this.Q<Label>("Hint");
            keyRoot = this.Q<VisualElement>("KeyRoot");

            initialized = true;
        }

        public void SetData(string? hintText, string? actionKeyText, string? iconClass)
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

            if (!string.IsNullOrEmpty(iconClass))
            {
                inputIcon.RemoveSprites();
                inputIcon.AddToClassList(iconClass);
                inputIcon.SetDisplayed(true);
            }
            else inputIcon.SetDisplayed(false);
        }

        public new class UxmlFactory : UxmlFactory<HoverCanvasTooltipElement> { }
    }
}
