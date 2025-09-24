using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Settings.Settings
{
    //Commented creator as we only need one of these.
    //[CreateAssetMenu(fileName = "ChatSettings", menuName = "DCL/Settings/Chat Settings")]
    public class ChatSettingsAsset : ScriptableObject
    {
        [FormerlySerializedAs("chatSettings")] public ChatAudioSettings chatAudioSettings = ChatAudioSettings.ALL;
        public ChatPrivacySettings chatPrivacySettings = ChatPrivacySettings.ALL;
        public ChatBubbleVisibilitySettings chatBubblesVisibilitySettings = ChatBubbleVisibilitySettings.ALL;

        public delegate void ChatPrivacyDelegate(ChatPrivacySettings privacySettings);
        public event ChatPrivacyDelegate? PrivacySettingsSet;
        public event ChatPrivacyDelegate? PrivacySettingsRead;

        public void OnPrivacySet(ChatPrivacySettings privacySettings)
        {
            chatPrivacySettings = privacySettings;
            PrivacySettingsSet?.Invoke(privacySettings);
        }

        public void OnPrivacyRead(ChatPrivacySettings privacySettings)
        {
            //IF response OK Update so we know to block non-friends messages as well and send them a response if they write to us so they update their settings
            // Controller needs to subscribe to both of these events
            chatPrivacySettings = privacySettings;
            PrivacySettingsRead?.Invoke(privacySettings);
        }

        public void SetBubblesVisibility(ChatBubbleVisibilitySettings bubblesSettings)
        {
            chatBubblesVisibilitySettings = bubblesSettings;
        }
    }

    public enum ChatAudioSettings
    {
        ALL = 0,
        MENTIONS_ONLY = 1,
        NONE = 2,
    }

    public enum ChatPrivacySettings
    {
        ONLY_FRIENDS = 0,
        ALL = 1,
    }

    public enum ChatBubbleVisibilitySettings
    {
        NONE = 0,
        NEARBY_ONLY = 1,
        ALL
    }
}
