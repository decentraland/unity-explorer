using DCL.UI.Controls.Configs;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Controls
{
    public class GenericContextMenuButtonWithTextView : GenericContextMenuSimpleButtonView
    {
        [field: SerializeField] public Image ImageComponent { get; private set; }

        [field: SerializeField] public float OpacityOnNonInteractable { get; private set; } = 0.2f;

        public void Configure(ButtonContextMenuControlSettings settings)
        {
            base.Configure(settings);
            ImageComponent.sprite = settings.buttonIcon;
            ImageComponent.color = settings.iconColor;
        }

        public override void SetAsInteractable(bool isInteractable)
        {
            base.SetAsInteractable(isInteractable);

            ImageComponent.color = new Color(ImageComponent.color.r, ImageComponent.color.g, ImageComponent.color.b, isInteractable ? 1 : OpacityOnNonInteractable);
            TextComponent.color = new Color(TextComponent.color.r, TextComponent.color.g, TextComponent.color.b, isInteractable ? 1 : OpacityOnNonInteractable);
        }
    }
}
