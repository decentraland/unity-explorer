using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Settings.Settings;
using DCL.VoiceChat;
using DCL.VoiceChat.CommunityVoiceChat;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace DCL.PluginSystem.Global
{
    //Commented creator as we only need one of these.
    //[CreateAssetMenu(fileName = "VoiceChatPluginSettings", menuName = "DCL/Voice Chat/Voice Chat Plugin settings")]
    public class VoiceChatPluginSettings : ScriptableObject
    {
        [Header("Asset References")]
        [field: SerializeField] public VoiceChatSettingsAsset VoiceChatSettings { get; private set; }
        [field: SerializeField] public VoiceChatConfiguration VoiceChatConfiguration { get; private set; }
        [field: FormerlySerializedAs("<PlayerEntryView>k__BackingField")] [field: SerializeField] public VoiceChatParticipantEntryView ParticipantEntryView { get; private set; }

        [Header("Audio References")]
        [field: SerializeField] public AudioClipConfig MuteMicrophoneAudio { get; private set; }
        [field: SerializeField] public AudioClipConfig UnmuteMicrophoneAudio { get; private set; }


    }
}
