using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.Utility.Extensions;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Utility;

namespace DCL.AuthenticationScreenFlow
{
    public class VerificationDappAuthView : ViewBase, IPointerClickHandler
    {
        [field: Space]
        [field: SerializeField]
        public Button BackButton { get; private set; } = null!;

        [Space]
        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("CODE HINT")]
        [SerializeField] private Button codeHintButton;
        [SerializeField] private GameObject codeHintContainer;

        [Space]
        [SerializeField] private TMP_Text code;
        [SerializeField] private LocalizeStringEvent countdown = null!;

        private StringVariable? countdownLabelParameter;
        private CancellationTokenSource? verificationCountdownCancellationToken;

        private int hideAnimHash = UIAnimationHashes.OUT;

        private void OnEnable()
        {
            codeHintContainer.SetActive(false);
            codeHintButton.onClick.AddListener(ToggleVerificationCodeVisibility);
        }

        private void OnDisable()
        {
            CancelVerificationCountdown();
            codeHintButton.onClick.RemoveAllListeners();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            codeHintContainer.SetActive(false);
        }

        public void Show(int dataCode, DateTime expiration)
        {
            code.text = dataCode.ToString();
            DoCountdownAsync(expiration).Forget();

            ShowAsync(CancellationToken.None).Forget();
        }

        public void Hide(int hideAnimHash)
        {
            this.hideAnimHash = hideAnimHash; //isBack ? UIAnimationHashes.BACK : UIAnimationHashes.OUT;
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

        private async UniTaskVoid DoCountdownAsync(DateTime expiration)
        {
            verificationCountdownCancellationToken = verificationCountdownCancellationToken.SafeRestart();

            countdownLabelParameter ??= (StringVariable)countdown.StringReference["time"];

            do
            {
                TimeSpan duration = expiration - DateTime.UtcNow;
                countdownLabelParameter.Value = $"{duration.Minutes:D2}:{duration.Seconds:D2}";
                await UniTask.Delay(1000, cancellationToken: verificationCountdownCancellationToken.Token);
            }
            while (expiration > DateTime.UtcNow);
        }

        private void CancelVerificationCountdown()
        {
            verificationCountdownCancellationToken?.SafeCancelAndDispose();
            verificationCountdownCancellationToken = null;
        }

        private void ToggleVerificationCodeVisibility() =>
            codeHintContainer.SetActive(!codeHintContainer.activeSelf);
    }
}
