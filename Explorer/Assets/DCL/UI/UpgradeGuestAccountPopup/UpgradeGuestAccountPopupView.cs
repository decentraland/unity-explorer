using DCL.UI.OTPInput;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.UpgradeGuestAccountPopup
{
    public class UpgradeGuestAccountPopupView : ViewBase, IView
    {
        [field: Header("Restricted User Step")]
        [field: SerializeField] public GameObject RestrictedUserRoot { get; private set; } = null!;
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button UpgradeAccountButton { get; private set; } = null!;

        [field: Header("Register Email Step")]
        [field: SerializeField] public GameObject RegisterEMailRoot { get; private set; } = null!;
        [field: SerializeField] public Button RegisterEMailCloseButton { get; private set; } = null!;
        [field: SerializeField] public EmailInputFieldView EMailInputField { get; private set; } = null!;

        [field: Header("Verify OTP Step")]
        [field: SerializeField] public GameObject VerifyOTPRoot { get; private set; } = null!;
        [field: SerializeField] public Button VerifyOTPCloseButton { get; private set; } = null!;
        [field: SerializeField] public Button VerifyOTPBackButton { get; private set; } = null!;
        [field: SerializeField] public OTPInputFieldView OTPInputField { get; private set; } = null!;
        [field: SerializeField] public Button ResendOTPButton { get; private set; } = null!;

        [field: Header("Verify Email Step")]
        [field: SerializeField] public GameObject VerifyEmailRoot { get; private set; } = null!;
        [field: SerializeField] public Button VerifyEmailCancelButton { get; private set; } = null!;

        [field: Header("Success Step")]
        [field: SerializeField] public GameObject SuccessRoot { get; private set; } = null!;
        [field: SerializeField] public Button ConfirmSuccessButton { get; private set; } = null!;

        [field: Header("Email already exist Step")]
        [field: SerializeField] public GameObject EMailFailedRoot { get; private set; } = null!;
        [field: SerializeField] public Button TryAnotherEmailButton { get; private set; } = null!;

        [field: Header("Error Step")]
        [field: SerializeField] public GameObject ErrorRoot { get; private set; } = null!;
        [field: SerializeField] public Button ErrorRetryButton { get; private set; } = null!;
    }
}
