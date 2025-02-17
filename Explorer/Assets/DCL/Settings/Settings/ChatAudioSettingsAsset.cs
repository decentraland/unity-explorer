using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Settings.Settings
{
    [CreateAssetMenu(fileName = "ChatAudioSettings", menuName = "DCL/Settings/Chat Audio Settings")]
    public class ChatAudioSettingsAsset : ScriptableObject
    {
        [FormerlySerializedAs("chatSettings")] public ChatAudioSettings chatAudioSettings = ChatAudioSettings.ALL;
    }

    public enum ChatAudioSettings
    {
        ALL = 0,
        MENTIONS_ONLY = 1,
        NONE = 2,
    }
}
