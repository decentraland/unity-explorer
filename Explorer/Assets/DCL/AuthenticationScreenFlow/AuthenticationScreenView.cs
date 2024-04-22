using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.CharacterPreview;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenView : ViewBase, IView
    {
        [SerializeField] private LocalizeStringEvent countdownLabel;

        private StringVariable? countdownLabelParameter;
        [field: SerializeField]
        public GameObject LoginContainer { get; private set; } = null!;

        [field: SerializeField]
        public Button LoginButton { get; private set; } = null!;

        [field: SerializeField]
        public GameObject ConnectingToServerContainer { get; private set; } = null!;

        [field: SerializeField]
        public GameObject Slides { get; private set; } = null!;

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

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; } = null!;

        [field: SerializeField]
        public Animator LoginAnimator { get; private set; } = null!;

        [field: SerializeField]
        public Animator VerificationAnimator { get; private set; } = null!;

        [field: SerializeField]
        public Animator FinalizeAnimator { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text VersionText { get; private set; } = null!;

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
