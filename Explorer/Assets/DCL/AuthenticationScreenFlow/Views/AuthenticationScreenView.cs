using DCL.CharacterPreview;
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
        public CharacterPreviewView CharacterPreviewView { get; private set; } = null!;

        [field: SerializeField]
        public Web3ConfirmationPopupView? TransactionFeeConfirmationView { get; private set; }

        [field: Header("SCREENS")]
        [field: SerializeField]
        public LoginSelectionAuthView LoginSelectionAuthView { get; private set; } = null!;

        [field: SerializeField]
        public VerificationDappAuthView VerificationDappAuthView { get; private set; } = null!;

        [field: SerializeField]
        public VerificationOTPAuthView VerificationOTPAuthView { get; private set; } = null!;

        [field: SerializeField]
        public ProfileFetchingAuthView ProfileFetchingAuthView { get; private set; } = null!;

        [field: SerializeField]
        public LobbyForExistingAccountAuthView LobbyForExistingAccountAuthView { get; private set; } = null!;
        [field: SerializeField]
        public LobbyForNewAccountAuthView LobbyForNewAccountAuthView { get; private set; } = null!;

        [field: Space]
        [field: SerializeField]
        public TMP_Text VersionText { get; private set; } = null!;

        [field: Header("BUTTONS")]
        [field: SerializeField]
        public Button DiscordButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ExitButton { get; private set; } = null!;

        [field: SerializeField]
        public MuteButtonView MuteButton { get; private set; } = null!;

        [field: SerializeField]
        public Button[] UseAnotherAccountButton { get; private set; } = null!;
    }
}
