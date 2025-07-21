using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuSubMenuButtonView : GenericContextMenuComponentBase, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField] public Button ButtonComponent { get; private set; }
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }
        [field: SerializeField] public Image ImageComponent { get; private set; }
        [field: SerializeField] public int UnHoverDebounceDurationMs { get; private set; } = 300;

        private ControlsContainerView container;
        private bool isHovering;
        private CancellationTokenSource hoverCts;

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
            hoverCts.SafeCancelAndDispose();
            UnregisterListeners();
        }

        public void Configure(SubMenuContextMenuButtonSettings settings)
        {
            TextComponent.SetText(settings.buttonText);
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            hoverCts.SafeCancelAndDispose();
            container.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            hoverCts = hoverCts.SafeRestart();
            WaitAndTriggerExitAsync(hoverCts.Token).Forget();
        }

        private async UniTaskVoid WaitAndTriggerExitAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(UnHoverDebounceDurationMs, cancellationToken: token);

                if (!isHovering)
                    container.gameObject.SetActive(false);
            }
            catch (OperationCanceledException) { }
        }
    }
}
