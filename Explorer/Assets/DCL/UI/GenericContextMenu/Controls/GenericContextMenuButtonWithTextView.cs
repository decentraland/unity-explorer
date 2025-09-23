using DCL.UI.Controls.Configs;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Controls
{
    public class GenericContextMenuButtonWithTextView : GenericContextMenuSimpleButtonView
    {
        [field: SerializeField] public Image ImageComponent { get; private set; }

        public void Configure(ButtonContextMenuControlSettings settings)
        {
            base.Configure(settings);
            ImageComponent.sprite = settings.buttonIcon;
            ImageComponent.color = settings.iconColor;
        }
    }
}
