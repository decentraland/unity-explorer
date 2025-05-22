using UnityEngine;

namespace DCL.VoiceChat
{
    public class VoiceChatView : MonoBehaviour
    {
        [field: SerializeField]
        public VoiceChatInCallView InCallView;

        [field: SerializeField]
        public VoiceChatIncomingCallView IncomingCallView;

        [field: SerializeField]
        public VoiceChatOutgoingCallView OutgoingCallView;
    }
}
