using DCL.Web3;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat
{
    public class VoiceChatView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject VoiceChatContainer;

        [field: SerializeField]
        public VoiceChatInCallView InCallView;

        [field: SerializeField]
        public VoiceChatIncomingCallView IncomingCallView;

        [field: SerializeField]
        public VoiceChatOutgoingCallView OutgoingCallView;

        private CancellationTokenSource cts;

        private void Start()
        {
            cts = new CancellationTokenSource();
        }

        public void SetActiveSection(VoiceChatStatus status, Web3Address walletId)
        {
            cts = cts.SafeRestart();
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    InCallView.gameObject.SetActive(true);
                    IncomingCallView.gameObject.SetActive(false);
                    OutgoingCallView.gameObject.SetActive(false);
                    InCallView.ProfileView.SetupAsync(walletId, cts.Token).Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL:
                    InCallView.gameObject.SetActive(false);
                    IncomingCallView.gameObject.SetActive(true);
                    OutgoingCallView.gameObject.SetActive(false);
                    IncomingCallView.ProfileView.SetupAsync(walletId, cts.Token).Forget();
                    break;
                case VoiceChatStatus.VOICE_CHAT_STARTED_CALL:
                    case VoiceChatStatus.VOICE_CHAT_STARTING_CALL:
                        InCallView.gameObject.SetActive(false);
                        IncomingCallView.gameObject.SetActive(false);
                        OutgoingCallView.gameObject.SetActive(true);
                        OutgoingCallView.ProfileView.SetupAsync(walletId, cts.Token).Forget();
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
