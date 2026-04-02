using DCL.UI.ProfileElements;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat
{
    public class VoiceChatOutgoingCallView : MonoBehaviour
    {
        [field: SerializeField]
        public MicrophoneButton MicrophoneButton;

        [field: SerializeField]
        public Button HangUpButton;

        [field: SerializeField]
        public SimpleProfileView ProfileView;
    }
}
