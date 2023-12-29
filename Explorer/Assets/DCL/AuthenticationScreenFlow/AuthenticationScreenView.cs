using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
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

        [SerializeField] private LocalizeStringEvent countdownLabel;

        public async UniTaskVoid StartVerificationCountdown(DateTime expiration, CancellationToken ct)
        {
            do
            {
                var timeParam = countdownLabel.StringReference["time"] as StringVariable;
                TimeSpan duration = expiration - DateTime.UtcNow;
                timeParam!.Value = $"{duration.Minutes:D2}:{duration.Seconds:D2}";
                await UniTask.Delay(1000, cancellationToken: ct);
            }
            while (expiration > DateTime.UtcNow);
        }
    }
}
