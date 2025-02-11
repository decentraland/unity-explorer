using UnityEngine;

namespace DCL.Settings.Settings
{
    public enum ChatSettings
    {
        All,
        Mentions,
        None
    }

    [CreateAssetMenu(fileName = "ChatAudioSettings", menuName = "DCL/Settings/Chat Audio Settings")]
    public class ChatAudioSettingsAsset : ScriptableObject
    {
        public ChatSettings chatSettings = ChatSettings.All;
    }
}
