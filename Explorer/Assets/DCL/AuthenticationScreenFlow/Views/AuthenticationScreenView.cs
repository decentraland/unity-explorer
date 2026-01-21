using DCL.UI.OTPInput;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenView : ViewBase, IView
    {
        [field: Header("SCREENS")]
        [field: SerializeField]
        public LoginSelectionAuthView LoginSelectionAuthView { get; private set; } = null!;

        [field: SerializeField]
        public VerificationDappAuthView VerificationDappAuthView { get; private set; } = null!;

        [field: SerializeField]
        public VerificationOTPAuthView VerificationOTPAuthView { get; private set; } = null!;

        [field: SerializeField]
        public LobbyForExistingAccountAuthView LobbyForExistingAccountAuthView { get; private set; } = null!;
        [field: SerializeField]
        public LobbyForNewAccountAuthView LobbyForNewAccountAuthView { get; private set; } = null!;

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
