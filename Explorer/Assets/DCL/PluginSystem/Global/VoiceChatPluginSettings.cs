using DCL.AssetsProvision;
using DCL.VoiceChat;
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
        [field: SerializeField] public VoiceChatSettingsReference VoiceChatSettings { get; private set; }
        [field: SerializeField] public MicrophoneAudioFilterReference MicrophoneAudioFilter { get; private set; }
        [field: SerializeField] public CombinedAudioSourceReference CombinedAudioSource { get; private set; }
        [field: SerializeField] public VoiceChatConfigurationReference VoiceChatConfiguration { get; private set; }


        [Serializable]
        public class VoiceChatSettingsReference : AssetReferenceT<DCL.Settings.Settings.VoiceChatSettingsAsset>
        {
            public VoiceChatSettingsReference(string guid) : base(guid) { }
        }

        [Serializable]
        public class CombinedAudioSourceReference : ComponentReference<VoiceChatCombinedStreamsAudioSource>
        {
            public CombinedAudioSourceReference(string guid) : base(guid) { }
        }

        [Serializable]
        public class MicrophoneAudioFilterReference : ComponentReference<VoiceChatMicrophoneAudioFilter>
        {
            public MicrophoneAudioFilterReference(string guid) : base(guid) { }
        }

        [Serializable]
        public class VoiceChatConfigurationReference : AssetReferenceT<VoiceChatConfiguration>
        {
            public VoiceChatConfigurationReference(string guid) : base(guid) { }
        }
    }
}
