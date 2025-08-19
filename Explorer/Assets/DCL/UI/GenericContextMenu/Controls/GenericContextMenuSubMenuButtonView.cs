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
        [field: SerializeField] public Image Arrow { get; private set; }
        [field: SerializeField] public int UnHoverDebounceDurationMs { get; private set; } = 300;
        [field: SerializeField] public RectTransform RightAnchor { get; private set; }
        [field: SerializeField] public RectTransform LeftAnchor { get; private set; }
        [field: SerializeField] private GameObject visibilityCalculationAnimator;

        internal ControlsContainerView container;
        private bool isHovering;
        private CancellationTokenSource hoverCts;
        private bool isButtonVisible;

        private Action containerConfigurationDelegate;

        public void SetContainerCreationMethod(Action containerConfigurationDelegate)
        {
            this.containerConfigurationDelegate = containerConfigurationDelegate;
        }

        public void SetContainer(ControlsContainerView container)
        {
            this.container = container;
            container.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            ButtonComponent.onClick.AddListener(() => ShowSubmenu(!container.gameObject.activeSelf));
        }

        private void OnDisable()
        {
            hoverCts.SafeCancelAndDispose();
            UnregisterListeners();
            container.gameObject.SetActive(false);
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
            RightAnchor.anchoredPosition = new Vector2(settings.anchorPadding, RightAnchor.anchoredPosition.y);
            LeftAnchor.anchoredPosition = new Vector2(-settings.anchorPadding, LeftAnchor.anchoredPosition.y);
            isButtonVisible = !settings.IsButtonAsynchronous;

            if (settings.IsButtonAsynchronous)
                ResolveVisibilityAsync(settings.asyncVisibilityResolverDelegate, CancellationToken.None).Forget(); // TODO
        }

        private async UniTaskVoid ResolveVisibilityAsync(SubMenuContextMenuButtonSettings.VisibilityResolverDelegate asyncVisibilityResolverDelegate, CancellationToken ct)
        {
            try
            {
                TextComponent.enabled = false;
                ImageComponent.enabled = false;
                Arrow.enabled = false;
                visibilityCalculationAnimator.SetActive(true);

                isButtonVisible = await asyncVisibilityResolverDelegate(ct);

                if(isHovering)
                    OnPointerEnter(null);
            }
            finally
            {
                gameObject.SetActive(isButtonVisible);
                TextComponent.enabled = true;
                ImageComponent.enabled = true;
                Arrow.enabled = true;
                visibilityCalculationAnimator.SetActive(false);
            }
        }

        public override void UnregisterListeners() =>
            ButtonComponent.onClick.RemoveAllListeners();

        public override void RegisterCloseListener(Action listener) {}

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            hoverCts.SafeCancelAndDispose();
            ShowSubmenu(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            hoverCts = hoverCts.SafeRestart();
            WaitAndTriggerExitAsync(hoverCts.Token).Forget();
        }

        private void ShowSubmenu(bool show)
        {
            if(show == container.gameObject.activeSelf || !isButtonVisible)
                return;

            container.gameObject.SetActive(show);

            // Asynchronous submenus are configured when shown
            if (show && containerConfigurationDelegate != null)
                containerConfigurationDelegate();
        }

        private async UniTaskVoid WaitAndTriggerExitAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(UnHoverDebounceDurationMs, cancellationToken: token);

                if (!isHovering)
                    ShowSubmenu(false);
            }
            catch (Exception) { }
        }
    }
}
