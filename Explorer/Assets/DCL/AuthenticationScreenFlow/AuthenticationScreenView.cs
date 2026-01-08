using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenView : ViewBase, IView, IPointerClickHandler
    {
        private StringVariable? countdownLabelParameter;

        [field: Header("LOGIN")]
        [field: SerializeField]
        public GameObject LoginContainer { get; private set; } = null!;

        [field: SerializeField]
        public Animator LoginAnimator { get; private set; } = null!;

        [field: SerializeField]
        public Button LoginButton { get; private set; } = null!;

        [field: SerializeField]
        public Button CancelLoginButton { get; private set; } = null!;

        [field: SerializeField]
        public TMP_InputField EmailInputField { get; private set; } = null!;

        [field: SerializeField]
        public Button LoginWithOtpButton { get; private set; } = null!;

        [field: SerializeField]
        public GameObject LoadingSpinner { get; private set; } = null!;

        [field: Header("CODE VERIFICATION")]
        [field: SerializeField]
        public GameObject VerificationContainer { get; private set; } = null!;

        [field: SerializeField]
        public Animator VerificationAnimator { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text VerificationDescriptionsLabel { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text VerificationCodeLabel { get; private set; } = null!;

        [field: SerializeField]
        public Button VerificationCodeHintButton { get; private set; } = null!;

        [field: SerializeField]
        public GameObject VerificationCodeHintContainer { get; private set; } = null!;
        [SerializeField]
        private LocalizeStringEvent countdownLabel = null!;

        [field: SerializeField]
        public Button CancelAuthenticationProcess { get; private set; } = null!;

        [field: Header("VERIFICATION OTP")]
        [field: SerializeField]
        public GameObject VerificationOTPContainer { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text DescriptionOTP { get; private set; } = null!;

        [field: SerializeField]
        public Animator VerificationOTPAnimator { get; private set; } = null!;

        [field: SerializeField]
        public OtpInputBox OTPInputField { get; private set; } = null!;

        [field: SerializeField]
        public Button CancelAuthenticationProcessOTP { get; private set; } = null!;

        [field: SerializeField]
        public Button ResendOTPButton { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text OTPSubmitResultText { get; private set; } = null!;

        [field: SerializeField]
        public GameObject OTPSubmitResultSucessIcon { get; private set; } = null!;

        [field: SerializeField]
        public GameObject OTPSubmitResultErrorIcon { get; private set; } = null!;

        [field: Header("FINALIZE")]
        [field: SerializeField]
        public GameObject FinalizeContainer { get; private set; } = null!;

        [field: SerializeField]
        public Animator FinalizeAnimator { get; private set; } = null!;

        [field: SerializeField]
        public Button JumpIntoWorldButton { get; private set; } = null!;

        [field: SerializeField]
        public GameObject Description { get; private set; } = null!;

        [field: SerializeField]
        public GameObject DiffAccountButton { get; private set; } = null!;

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; } = null!;

        [field: SerializeField]
        public LocalizeStringEvent ProfileNameLabel { get; private set; } = null!;

        [field: Space]
        [field: SerializeField]
        public GameObject NewUserContainer { get; private set; } = null!;

        [field: SerializeField]
        public TMP_InputField ProfileNameInputField { get; private set; } = null!;

        [field: SerializeField]
        public Button BackButton { get; private set; } = null!;
        [field: SerializeField]
        public Button FinalizeNewUserButton { get; private set; } = null!;

        [field: SerializeField]
        public Button PrevRandomButton { get; private set; } = null!;
        [field: SerializeField]
        public Button NextRandomButton { get; private set; } = null!;
        [field: SerializeField]
        public Button RandomizeButton { get; private set; } = null!;

        [field: SerializeField]
        public Toggle SubscribeToggle { get; private set; } = null!;
        [field: SerializeField]
        public Toggle AgreeLicenseToggle { get; private set; } = null!;

        [field: Header("ERROR POPUP")]
        [field: SerializeField]
        public GameObject ErrorPopupRoot { get; private set; } = null!;

        [field: SerializeField]
        public Button ErrorPopupRetryButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ErrorPopupExitButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ErrorPopupCloseButton { get; private set; } = null!;

        [field: SerializeField]
        public Button[] UseAnotherAccountButton { get; private set; } = null!;

        [field: Header("OTHER")]
        [field: SerializeField]
        public Button DiscordButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ExitButton { get; private set; } = null!;

        [field: SerializeField]
        public MuteButtonView MuteButton { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text VersionText { get; private set; } = null!;

        [field: SerializeField]
        public GameObject RestrictedUserContainer { get; private set; } = null!;

        [field: SerializeField]
        public Button RequestAlphaAccessButton { get; private set; } = null!;

        public async UniTaskVoid StartVerificationCountdownAsync(DateTime expiration, CancellationToken ct)
        {
            do
            {
                countdownLabelParameter ??= (StringVariable)countdownLabel.StringReference["time"];
                TimeSpan duration = expiration - DateTime.UtcNow;
                countdownLabelParameter.Value = $"{duration.Minutes:D2}:{duration.Seconds:D2}";
                await UniTask.Delay(1000, cancellationToken: ct);
            }
            while (expiration > DateTime.UtcNow);
        }

        public void ShakeOtpInputField()
        {
            // To be implemented
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            VerificationCodeHintContainer.SetActive(false);
        }

        private void OnDisable()
        {
            LoginAnimator.enabled = false;
            VerificationAnimator.enabled = false;
            FinalizeAnimator.enabled = false;
        }

        private void OnEnable()
        {
            LoginAnimator.enabled = true;
            VerificationAnimator.enabled = true;
            FinalizeAnimator.enabled = true;
        }
    }
}
