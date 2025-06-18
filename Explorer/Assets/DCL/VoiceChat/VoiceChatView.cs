using DCL.Audio;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat
{
    public class VoiceChatView : MonoBehaviour
    {
        private const float SHOW_HIDE_ANIMATION_DURATION = 0.5f;

        [field: SerializeField]
        public CanvasGroup VoiceChatCanvasGroup { get; private set; }

        [field: SerializeField]
        public GameObject VoiceChatContainer;

        [field: SerializeField]
        public VoiceChatInCallView InCallView;

        [field: SerializeField]
        public VoiceChatIncomingCallView IncomingCallView;

        [field: SerializeField]
        public VoiceChatOutgoingCallView OutgoingCallView;

        [field: SerializeField]
        public VoiceChatErrorView ErrorView;

        [field: SerializeField]
        public AudioClipConfig MuteMicrophoneAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig UnMuteMicrophoneAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig JoinCallAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig LeaveCallAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig CallTuneAudio { get; private set; }

        private CancellationTokenSource cts;

        private void Start()
        {
            cts = new CancellationTokenSource();
        }

        public void Show()
        {
            VoiceChatContainer.SetActive(true);
            VoiceChatCanvasGroup.alpha = 0;
            VoiceChatCanvasGroup
                .DOFade(1, SHOW_HIDE_ANIMATION_DURATION)
                .SetEase(Ease.Flash)
                .OnComplete(() =>
                {
                    VoiceChatContainer.SetActive(true);
                    VoiceChatCanvasGroup.alpha = 1;
                });
        }

        public void Hide()
        {
            VoiceChatCanvasGroup.alpha = 1;
            VoiceChatCanvasGroup
                .DOFade(0, SHOW_HIDE_ANIMATION_DURATION)
                .SetEase(Ease.Flash)
                .OnComplete(() =>
                {
                    VoiceChatContainer.SetActive(false);
                    VoiceChatCanvasGroup.alpha = 0;
                });
        }

        public void SetActiveSection(VoiceChatStatus status, Web3Address walletId, ProfileRepositoryWrapper profileDataProvider)
        {
            cts = cts.SafeRestart();
            DisableAllSections();
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    InCallView.gameObject.SetActive(true);
                    InCallView.ProfileView.SetupAsync(walletId, profileDataProvider, cts.Token).Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL:
                    IncomingCallView.SetActive(true);
                    IncomingCallView.ProfileView.SetupAsync(walletId, profileDataProvider, cts.Token).Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_STARTED_CALL:
                    OutgoingCallView.gameObject.SetActive(true);
                    OutgoingCallView.ProfileView.SetupAsync(walletId, profileDataProvider, cts.Token).Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    ErrorView.SetActive(true);
                    ErrorView.StartErrorPanelDisableFlow();
                    break;
            }
        }

        private void DisableAllSections()
        {
            InCallView.gameObject.SetActive(false);
            IncomingCallView.gameObject.SetActive(false);
            OutgoingCallView.gameObject.SetActive(false);
            ErrorView.SetActive(false);
        }
    }
}
