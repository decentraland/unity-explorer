using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.Utility.Extensions;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    [RequireComponent(typeof(Animator), typeof(CanvasGroup))]
    public class LoginSelectionAuthView : ViewBase
    {
        [field: Space]
        [field: SerializeField]
        public Button CancelLoginButton { get; private set; } = null!;

        [field: Header("PRIMARY LOGIN")]
        [field: SerializeField]
        public EmailInputFieldView EmailInputField { get; private set; } = null!;

        [field: Header("SECONDARY LOGINS")]
        [field: SerializeField]
        public Button LoginMetamaskButton { get; private set; } = null!;

        [field: SerializeField]
        public Button LoginGoogleButton { get; private set; } = null!;

        [field: SerializeField]
        public Button LoginDiscordButton { get; private set; } = null!;

        [field: SerializeField]
        public Button LoginAppleButton { get; private set; } = null!;

        [field: SerializeField]
        public Button LoginXButton { get; private set; } = null!;

        [field: SerializeField]
        public Button LoginFortmaticButton { get; private set; } = null!;

        [field: SerializeField]
        public Button LoginCoinbaseButton { get; private set; } = null!;

        [field: SerializeField]
        public Button LoginWalletConnectButton { get; private set; } = null!;

        [field: Header("OTHER OPTIONS")]
        [field: SerializeField]
        public Button MoreOptionsButton { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform MoreOptionsButtonDirIcon { get; private set; } = null!;

        [SerializeField] private GameObject moreOptionsPanel;

        [Space]
        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField] private GameObject loadingSpinner;
        [SerializeField] private GameObject mainElementsPanel;

        private int showAnimHash = UIAnimationHashes.OUT;
        private bool areOptionsExpanded;

        public void ToggleOptionsPanelExpansion()
        {
            areOptionsExpanded = !areOptionsExpanded;
            SetOptionsPanelVisibility(areOptionsExpanded);
        }

        private void SetOptionsPanelVisibility(bool isExpanded)
        {
            MoreOptionsButtonDirIcon.localScale = new Vector3(1, isExpanded ? -1 : 1, 1);
            moreOptionsPanel.SetActive(isExpanded);
        }

        public void Show(int animHash)
        {
            showAnimHash = animHash;
            ShowAsync(CancellationToken.None).Forget();

            mainElementsPanel.SetActive(true);
            loadingSpinner.SetActive(false);

            areOptionsExpanded = false;
            SetOptionsPanelVisibility(isExpanded: false);
        }

        public void Hide()
        {
            mainElementsPanel.SetActive(false);
            loadingSpinner.SetActive(false);

            HideAsync(CancellationToken.None).Forget();
        }

        public override async UniTask ShowAsync(CancellationToken ct)
        {
            await base.ShowAsync(ct);
            canvasGroup.interactable = true;
        }

        public override async UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            canvasGroup.interactable = false;
            await base.HideAsync(ct, isInstant);
        }

        protected override async UniTask PlayShowAnimationAsync(CancellationToken ct) =>
            await animator.PlayAndAwaitAsync(showAnimHash, showAnimHash, ct: ct);

        protected override async UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            await animator.PlayAndAwaitAsync(UIAnimationHashes.OUT, UIAnimationHashes.OUT, ct: ct);

        public void ShowLoading()
        {
            mainElementsPanel.SetActive(false);
            loadingSpinner.SetActive(true);
        }
    }
}
