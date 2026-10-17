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
    public class LobbyForNewAccountAuthView : ViewBase
    {
        [field: Space]
        [field: SerializeField]
        public NameInputFieldView ProfileNameInputField { get; private set; } = null!;

        [field: SerializeField]
        public Button BackButton { get; private set; } = null!;

        [field: SerializeField]
        public Button FinalizeNewUserButton { get; private set; } = null!;

        [field: SerializeField]
        public GameObject JumpInIcon { get; private set; } = null!;

        [field: SerializeField]
        public GameObject FinalizeLoading { get; private set; } = null!;

        [field: Space]
        [field: SerializeField]
        public GameObject SubscribeRoot { get; private set; } = null!;
        [field: SerializeField]
        public Toggle SubscribeToggle { get; private set; } = null!;
        [field: SerializeField]
        public Toggle TermsOfUse { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text_ClickeableLink TermsOfUseAndPrivacyLink { get; private set; } = null!;

        [Space]
        [SerializeField] private Animator animator = null!;
        [SerializeField] private CanvasGroup canvasGroup = null!;

        private int hideAnimHash = UIAnimationHashes.OUT;

        public void Show()
        {
            ShowAsync(CancellationToken.None).Forget();
        }

        public void Hide(int animHash)
        {
            hideAnimHash = animHash;
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
            await animator.PlayAndAwaitAsync(UIAnimationHashes.IN, UIAnimationHashes.IN, ct: ct);

        protected override async UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            await animator.PlayAndAwaitAsync(hideAnimHash, hideAnimHash, ct: ct);
    }
}
