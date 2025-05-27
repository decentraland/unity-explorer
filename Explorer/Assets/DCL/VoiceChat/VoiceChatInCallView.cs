using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat
{
    public class VoiceChatInCallView : MonoBehaviour
    {
        [field: SerializeField]
        public MicrophoneButton MicrophoneButton;

        [field: SerializeField]
        public Button HangUpButton;
    }
}
