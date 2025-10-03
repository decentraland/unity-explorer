using DCL.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatInCallButtonsView : MonoBehaviour
    {
        [field: SerializeField] public MicrophoneButton MicrophoneButton  { get; private set; }
        [field: SerializeField] public Button EndCallButton  { get; private set; }
        [field: SerializeField] public Button? LeaveStageButton  { get; private set; }
        [field: SerializeField] public Button RaiseHandButton  { get; private set; }
        [field: SerializeField] public Button LowerHandButton  { get; private set; }
        [field: SerializeField] public AudioClipConfig UnMuteAudio  { get; private set; }
        [field: SerializeField] public AudioClipConfig MuteAudio  { get; private set; }
    }
}
