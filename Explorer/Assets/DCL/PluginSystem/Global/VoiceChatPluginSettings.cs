using DCL.Audio;
using DCL.VoiceChat;
using DCL.VoiceChat.CommunityVoiceChat;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    //Commented creator as we only need one of these.
    //[CreateAssetMenu(fileName = "VoiceChatPluginSettings", menuName = "DCL/Voice Chat/Voice Chat Plugin settings")]
    public class VoiceChatPluginSettings : ScriptableObject
    {
        [Header("Asset References")]
        [field: SerializeField] public VoiceChatConfiguration VoiceChatConfiguration { get; private set; } = null!;
        [field: SerializeField] public PlayerEntryView PlayerEntryView { get; private set; } = null!;

        [Header("Audio References")]
        [field: SerializeField] public AudioClipConfig MuteMicrophoneAudio { get; private set; } = null!;
        [field: SerializeField] public AudioClipConfig UnmuteMicrophoneAudio { get; private set; } = null!;
    }
}
