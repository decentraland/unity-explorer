using DCL.UI.GenericContextMenu.Controls.Configs;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuToggleWithIconAndCheckView : GenericContextMenuToggleWithCheckView
    {
        [Header("Icon-Specific References")]
        [SerializeField] private Image iconImage;

        public void Configure(ToggleWithIconAndCheckContextMenuControlSettings settings, bool initialValue)
        {
            textComponent.SetText(settings!.toggleText);
            toggleComponent.group = settings.toggleGroup;
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;
            HorizontalLayoutComponent.spacing = settings.horizontalLayoutSpacing;
            HorizontalLayoutComponent.reverseArrangement = settings.horizontalLayoutReverseArrangement;
            toggleComponent.SetIsOnWithoutNotify(initialValue);
            RegisterListener(settings.callback);

            bool hasIcon = settings.icon != null;

            var iconObject = iconImage.gameObject;
            iconObject.SetActive(hasIcon);

            if (hasIcon)
            {
                iconImage.sprite = settings.icon;
                iconImage.color = settings.iconColor;
            }
        }
    }
}