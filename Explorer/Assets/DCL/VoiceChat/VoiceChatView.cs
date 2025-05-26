using UnityEngine;

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

        public void SetActiveSection(VoiceChatStatus status)
        {
            InCallView.gameObject.SetActive(status == VoiceChatStatus.VOICE_CHAT_IN_CALL);
            IncomingCallView.gameObject.SetActive(status == VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL);
            OutgoingCallView.gameObject.SetActive(status is VoiceChatStatus.VOICE_CHAT_STARTED_CALL or VoiceChatStatus.VOICE_CHAT_STARTING_CALL);
        }
    }
}
