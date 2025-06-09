using DCL.Audio;
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
        public AudioClipConfig MuteMicrophoneAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig UnMuteMicrophoneAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig JoinCallAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig LeaveCallAudio { get; private set; }

        private CancellationTokenSource cts;

        private void Start()
        {
            cts = new CancellationTokenSource();
        }

        public void Show(VoiceChatStatus status, Web3Address walletId)
        {
            VoiceChatContainer.SetActive(true);
            VoiceChatCanvasGroup.alpha = 0;
            VoiceChatCanvasGroup
                .DOFade(1, SHOW_HIDE_ANIMATION_DURATION)
                .SetEase(Ease.Flash);
            SetActiveSection(status, walletId);
        }

        public void Hide(VoiceChatStatus status, Web3Address walletId)
        {
            VoiceChatCanvasGroup.alpha = 1;
            VoiceChatCanvasGroup
                .DOFade(0, SHOW_HIDE_ANIMATION_DURATION)
                .SetEase(Ease.Flash)
                .OnComplete(() =>
                {
                    VoiceChatContainer.SetActive(false);
                    SetActiveSection(status, walletId);
                });
        }

        private void SetActiveSection(VoiceChatStatus status, Web3Address walletId)
        {
            cts = cts.SafeRestart();
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    InCallView.gameObject.SetActive(true);
                    IncomingCallView.SetActive(false);
                    OutgoingCallView.gameObject.SetActive(false);
                    //InCallView.ProfileView.SetupAsync(walletId, cts.Token).Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL:
                    InCallView.gameObject.SetActive(false);
                    IncomingCallView.SetActive(true);
                    OutgoingCallView.gameObject.SetActive(false);
                    //IncomingCallView.ProfileView.SetupAsync(walletId, cts.Token).Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_STARTED_CALL:
                    case VoiceChatStatus.VOICE_CHAT_STARTING_CALL:
                        InCallView.gameObject.SetActive(false);
                        IncomingCallView.SetActive(false);
                        OutgoingCallView.gameObject.SetActive(true);
                        //OutgoingCallView.ProfileView.SetupAsync(walletId, cts.Token).Forget();
                        break;
                default:
                    InCallView.gameObject.SetActive(false);
                    IncomingCallView.gameObject.SetActive(false);
                    OutgoingCallView.gameObject.SetActive(false);
                    break;
            }
        }
    }
}
