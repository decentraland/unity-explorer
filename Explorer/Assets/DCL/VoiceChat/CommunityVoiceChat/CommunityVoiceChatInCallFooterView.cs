using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatInCallFooterView : MonoBehaviour
    {
        [field: SerializeField] public MicrophoneButton MicrophoneButton  { get; private set; }
        [field: SerializeField] public Button EndCallButton  { get; private set; }
        [field: SerializeField] public Button LeaveStageButton  { get; private set; }
        [field: SerializeField] public Button RaiseHandButton  { get; private set; }
    }
}
