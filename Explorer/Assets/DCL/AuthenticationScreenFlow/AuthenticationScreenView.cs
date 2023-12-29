using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenView : ViewBase, IView
    {
        [field: SerializeField]
        public GameObject SplashContainer { get; private set; } = null!;

        [field: SerializeField]
        public VideoPlayer SplashVideoPlayer { get; private set; } = null!;

        [field: SerializeField]
        public GameObject LoginContainer { get; private set; } = null!;

        [field: SerializeField]
        public Button LoginButton { get; private set; } = null!;

        [field: SerializeField]
        public GameObject ConnectingToServerContainer { get; private set; } = null!;

        [field: SerializeField]
        public GameObject PendingAuthentication { get; private set; } = null!;

        [field: SerializeField]
        public Button CancelAuthenticationProcess { get; private set; } = null!;

        [field: SerializeField]
        public GameObject ProgressContainer { get; private set; } = null!;

        [field: SerializeField]
        public GameObject FinalizeContainer { get; private set; } = null!;

        [field: SerializeField]
        public Button JumpIntoWorldButton { get; private set; } = null!;

        [field: SerializeField]
        public Button UseAnotherAccountButton { get; private set; } = null!;

        [field: SerializeField]
        public LocalizeStringEvent ProfileNameLabel { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text VerificationCodeLabel { get; private set; } = null!;

        [field: SerializeField]
        public Button VerificationCodeHintButton { get; private set; } = null!;

        [field: SerializeField]
        public GameObject VerificationCodeHintContainer { get; private set; } = null!;

        [field: SerializeField]
        public Button DiscordButton { get; private set; } = null!;
    }
}
