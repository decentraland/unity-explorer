using DCL.Chat.History;
using DCL.Prefs;
using DCL.Settings.Settings;

namespace DCL.Chat
{
    /// <summary>
    ///
    /// </summary>
    public static class ChatUserSettings
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="currentChannelId"></param>
        /// <returns></returns>
        public static ChatAudioSettings GetNotificationPingValuePerChannel(ChatChannel.ChannelId currentChannelId)
        {
            int defaultNotificationPingValue = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_CHAT_SOUNDS); // General settings
            return (ChatAudioSettings)DCLPlayerPrefs.GetInt(GetCurrentChannelSettingsKey(DCLPrefKeys.SETTINGS_CHAT_SOUNDS, currentChannelId), defaultNotificationPingValue);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="selectedMode"></param>
        /// <param name="currentChannelId"></param>
        public static void SetNotificationPintValuePerChannel(ChatAudioSettings selectedMode, ChatChannel.ChannelId currentChannelId)
        {
            DCLPlayerPrefs.SetInt(GetCurrentChannelSettingsKey(DCLPrefKeys.SETTINGS_CHAT_SOUNDS, currentChannelId), (int)selectedMode, save: true);
        }

        // Decorates a property key so it can be properly stored/loaded in player prefs for the current user and channel
        private static string GetCurrentChannelSettingsKey(string propertyKey, ChatChannel.ChannelId currentChannelId)
        {
            // TODO: The player prefs class should provide a way to save date per user in the same computer. Otherwise, the Id of the user will have to be concatenated to the key
            return currentChannelId.Id + propertyKey;
        }
    }
}
