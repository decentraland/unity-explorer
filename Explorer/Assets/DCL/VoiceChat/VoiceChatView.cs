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
        public VoiceChatConnectingView ConnectingView;

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
        private Sequence? isSpeakingCurrentSequence;

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

        public void SetSpeakingStatus(int speakingCount, string userName)
        {
            InCallView.PeopleTalkingContainer.gameObject.SetActive(speakingCount >= 1);
            InCallView.MultiplePeopleTalking.gameObject.SetActive(speakingCount > 1);
            InCallView.PlayerNameTalking.gameObject.SetActive(speakingCount == 1);
            InCallView.NoPlayerTalking.gameObject.SetActive(speakingCount == 0);

            if (speakingCount >= 1)
            {
                isSpeakingCurrentSequence = DOTween.Sequence();
                isSpeakingCurrentSequence.Append(InCallView.isSpeakingIconRect.DOScaleY(0.2f, SHOW_HIDE_ANIMATION_DURATION));
                isSpeakingCurrentSequence.Join(InCallView.isSpeakingIconOuterRect.DOScaleY(1, SHOW_HIDE_ANIMATION_DURATION));
                isSpeakingCurrentSequence.Append(InCallView.isSpeakingIconOuterRect.DOScaleY(0.2f, SHOW_HIDE_ANIMATION_DURATION));
                isSpeakingCurrentSequence.Join(InCallView.isSpeakingIconRect.DOScaleY(1, SHOW_HIDE_ANIMATION_DURATION));
                isSpeakingCurrentSequence.SetLoops(-1);
                isSpeakingCurrentSequence.Play();
            }
            else
            {
                isSpeakingCurrentSequence.Kill();
            }
        }

        public void SetActiveSection(VoiceChatStatus status, Web3Address walletId, ProfileRepositoryWrapper profileDataProvider)
        {
            cts = cts.SafeRestart();
            DisableAllSections();
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    ConnectingView.gameObject.SetActive(true);
                    InCallView.ProfileView.SetupAsync(walletId, profileDataProvider, cts.Token).Forget();
                    ConnectingView.ProfileView.SetupAsync(walletId, profileDataProvider, cts.Token).Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL:
                    IncomingCallView.SetActive(true);
                    IncomingCallView.ProfileView.SetupAsync(walletId, profileDataProvider, cts.Token).Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_STARTED_CALL:
                    OutgoingCallView.gameObject.SetActive(true);
                    OutgoingCallView.ProfileView.SetupAsync(walletId, profileDataProvider, cts.Token).Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_USER_BUSY:
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    ErrorView.SetActive(true);
                    ErrorView.StartErrorPanelDisableFlow();
                    break;
            }
        }

        public void SetInCallSection()
        {
            DisableAllSections();
            InCallView.gameObject.SetActive(true);
        }

        private void DisableAllSections()
        {
            InCallView.gameObject.SetActive(false);
            IncomingCallView.gameObject.SetActive(false);
            OutgoingCallView.gameObject.SetActive(false);
            ErrorView.SetActive(false);
            ConnectingView.gameObject.SetActive(false);
        }
    }
}
