using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Settings.Settings
{
    //Commented creator as we only need one of these.
    //[CreateAssetMenu(fileName = "ChatSettings", menuName = "DCL/Settings/Chat Settings")]
    public class ChatSettingsAsset : ScriptableObject
    {
        [FormerlySerializedAs("chatSettings")] public ChatAudioSettings chatAudioSettings = ChatAudioSettings.All;
        public ChatPrivacySettings chatPrivacySettings = ChatPrivacySettings.All;
        public ChatBubbleVisibilitySettings chatBubblesVisibilitySettings = ChatBubbleVisibilitySettings.All;
        public ChatPreferredTranslationSettings chatPreferredTranslationSettings = ChatPreferredTranslationSettings.En;
        public bool chatReactionsEnabled = true;

        public string CHAT_TRANSLATION_SETTINGS_HOVER_TOOLTIP
            = "Chat messages will be translated into the language you select in this setting.";
        public delegate void ChatPrivacyDelegate(ChatPrivacySettings privacySettings);
        public delegate void ChatReactionsEnabledDelegate(bool enabled);
        public delegate void ChatBubblesVisibilityDelegate(ChatBubbleVisibilitySettings settings);

        public event ChatReactionsEnabledDelegate? ChatReactionsEnabledChanged;
        public event ChatBubblesVisibilityDelegate? BubblesVisibilityChanged;

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
            BubblesVisibilityChanged?.Invoke(bubblesSettings);
        }

        public void SetReactionsEnabled(bool enabled)
        {
            if (chatReactionsEnabled == enabled) return;

            chatReactionsEnabled = enabled;
            ChatReactionsEnabledChanged?.Invoke(enabled);
        }
    }

    public enum ChatAudioSettings
    {
        All = 0,
        MentionsOnly = 1,
        None = 2,
    }

    public enum ChatPrivacySettings
    {
        OnlyFriends = 0,
        All = 1,
    }

    public enum ChatBubbleVisibilitySettings
    {
        None = 0,
        NearbyOnly = 1,
        All
    }

    public enum ChatPreferredTranslationSettings
    {
        En = 0,
        Es = 1,
        Fr = 2,
        De = 3,
        Ru = 4,
        Pt = 5,
        It = 6,
        Zh = 7,
        Ja = 8,
        Ko = 9
    }
}
