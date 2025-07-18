using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuSubMenuButtonView : GenericContextMenuComponentBase
    {
        [field: SerializeField] public Button ButtonComponent { get; private set; }
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }
        [field: SerializeField] public Image ImageComponent { get; private set; }

        private ControlsContainerView container;

        public void SetContainer(ControlsContainerView container)
        {
            this.container = container;
            container.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            ButtonComponent.onClick.AddListener(() => container.gameObject.SetActive(!container.gameObject.activeSelf));
        }

        private void OnDisable()
        {
            UnregisterListeners();
        }

        public void Configure(SubMenuContextMenuButtonSettings settings)
        {
            TextComponent.SetText(settings!.buttonText);
            TextComponent.color = settings.textColor;
            ImageComponent.sprite = settings.buttonIcon;
            ImageComponent.color = settings.iconColor;
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;
            HorizontalLayoutComponent.spacing = settings.horizontalLayoutSpacing;
            HorizontalLayoutComponent.reverseArrangement = settings.horizontalLayoutReverseArrangement;
        }

        public override void UnregisterListeners() =>
            ButtonComponent.onClick.RemoveAllListeners();

        public override void RegisterCloseListener(Action listener) {}

    }
}
