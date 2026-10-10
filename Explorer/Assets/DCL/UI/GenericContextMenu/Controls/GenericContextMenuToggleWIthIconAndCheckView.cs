using DCL.UI.Controls.Configs;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Controls
{
    public class GenericContextMenuToggleWithIconAndCheckView : GenericContextMenuToggleWithCheckView
    {
        [Header("Icon-Specific References")]
        [SerializeField] private Image iconImage;

        [SerializeField] private RectTransform onBackground;
        [SerializeField] private RectTransform offBackground;

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

            OnToggleChanged(initialValue);
            toggleComponent.onValueChanged.AddListener(OnToggleChanged);
        }

        private void OnToggleChanged(bool isOn)
        {
            onBackground.gameObject.SetActive(!isOn);
            offBackground.gameObject.SetActive(isOn);
        }
    }
}