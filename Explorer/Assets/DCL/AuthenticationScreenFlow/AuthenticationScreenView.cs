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

        private StringVariable? countdownLabelParameter;

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
    }
}
