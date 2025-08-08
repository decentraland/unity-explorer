using DCL.AssetsProvision;
using DCL.Audio;
using DCL.VoiceChat;
using DCL.VoiceChat.CommunityVoiceChat;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    //Commented creator as we only need one of these.
    //[CreateAssetMenu(fileName = "VoiceChatPluginSettings", menuName = "DCL/Voice Chat/Voice Chat Plugin settings")]
    public class VoiceChatPluginSettings : ScriptableObject
    {
        [Header("Asset References")]
        [field: SerializeField] public StaticSettings.VoiceChatSettingsRef VoiceChatSettings { get; private set; }
        [field: SerializeField] public CombinedAudioSourceReference CombinedAudioSource { get; private set; }
        [field: SerializeField] public VoiceChatConfigurationReference VoiceChatConfiguration { get; private set; }
        [field: SerializeField] public PlayerEntryViewRef PlayerEntryView { get; private set; }

        [Header("Audio References")]
        [field: SerializeField] public AudioClipConfigReference MuteMicrophoneAudio { get; private set; }
        [field: SerializeField] public AudioClipConfigReference UnmuteMicrophoneAudio { get; private set; }

        [Serializable]
        public class CombinedAudioSourceReference : ComponentReference<VoiceChatCombinedStreamsAudioSource>
        {
            public CombinedAudioSourceReference(string guid) : base(guid) { }
        }

        [Serializable]
        public class VoiceChatConfigurationReference : AssetReferenceT<VoiceChatConfiguration>
        {
            public VoiceChatConfigurationReference(string guid) : base(guid) { }
        }

        [Serializable]
        public class AudioClipConfigReference : AssetReferenceT<AudioClipConfig>
        {
            public AudioClipConfigReference(string guid) : base(guid) { }
        }

        [Serializable]
        public class PlayerEntryViewRef : ComponentReference<PlayerEntryView>
        {
            public PlayerEntryViewRef(string guid) : base(guid) { }
        }
    }
}
