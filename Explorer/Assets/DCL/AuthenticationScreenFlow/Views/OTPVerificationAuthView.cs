using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.UI.OTPInput;
using DCL.Utility.Extensions;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class OTPVerificationAuthView : ViewBase
    {
        [field: Space]
        [field: SerializeField]
        public OTPInputFieldView InputField { get; private set; } = null!;

        [field: SerializeField]
        public Button BackButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ResendCodeButton { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text VerificationResultText { get; private set; } = null!;

        [field: SerializeField]
        public GameObject VerificationSuccessIcon { get; private set; } = null!;

        [field: SerializeField]
        public GameObject VerificationErrorIcon { get; private set; } = null!;

        private int hideAnimHash = UIAnimationHashes.OUT;

        [Space]
        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text description;

        public void Show(string email)
        {
            ShowAsync(CancellationToken.None).Forget();

            description.text = description.text.Replace("your@email.com", email); // Update description with user email
            VerificationResultText.gameObject.SetActive(false); // Reset OTP result UI
        }

        public void Hide(bool isBack = false)
        {
            hideAnimHash = isBack ? UIAnimationHashes.BACK : UIAnimationHashes.OUT;
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
            InputField.Clear();
        }

        protected override async UniTask PlayShowAnimationAsync(CancellationToken ct) =>
            await animator.PlayAndAwaitAsync(UIAnimationHashes.IN, UIAnimationHashes.IN, ct: ct);

        protected override async UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            await animator.PlayAndAwaitAsync(hideAnimHash, hideAnimHash, ct: ct);
    }
}
