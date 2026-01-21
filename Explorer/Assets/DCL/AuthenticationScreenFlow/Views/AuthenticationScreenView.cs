using DCL.UI.OTPInput;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenView : ViewBase, IView
    {
        [field: Space]
        [field: SerializeField]
        public LoginScreenSubView LoginScreenSubView { get; private set; } = null!;

        [field: SerializeField]
        public DappVerificationAuthView DappVerificationAuthView { get; private set; } = null!;

        [field: SerializeField]
        public ExistingAccountLobbyScreenSubView ExistingAccountLobbyScreenSubView { get; private set; } = null!;
        [field: SerializeField]
        public NewAccountLobbyScreenSubView NewAccountLobbyScreenSubView { get; private set; } = null!;

        [field: Header("VERIFICATION OTP")]
        [field: SerializeField]
        public GameObject VerificationOTPContainer { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text DescriptionOTP { get; private set; } = null!;

        [field: SerializeField]
        public Animator VerificationOTPAnimator { get; private set; } = null!;

        [field: SerializeField]
        public OTPInputFieldView OTPInputField { get; private set; } = null!;

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

        [field: Header("CONFIRMATION POPUP")]
        [field: SerializeField]
        public GameObject ConfPopupRoot { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text ConfPopupRootText { get; private set; } = null!;

        [field: SerializeField]
        public Button ConfPopupRootConfirmButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ConfPopupRootCancelButton { get; private set; } = null!;

        [field: Header("TRANSACTION FEE CONFIRMATION")]
        [field: SerializeField]
        public TransactionFeeConfirmationView? TransactionFeeConfirmationView { get; private set; }

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
    }
}
