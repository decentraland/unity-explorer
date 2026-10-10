using DCL.Chat.History;
using DCL.Prefs;
using DCL.Settings.Settings;

namespace DCL.Chat
{
    /// <summary>
    /// Provides a unified way to modify the settings related to the chat from the main controller.
    /// </summary>
    public static class ChatUserSettings
    {
        /// <summary>
        /// Gets the selected option for the Notification ping sound behaviour, for a given chat conversation.
        /// </summary>
        /// <param name="channelId">The id of the conversation.</param>
        /// <returns>The current value of the setting. If it does not exist, the value of the global Notification ping setting is returned instead.</returns>
        public static ChatAudioSettings GetNotificationPingValuePerChannel(ChatChannel.ChannelId channelId)
        {
            int defaultNotificationPingValue = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_CHAT_SOUNDS); // General settings
            return (ChatAudioSettings)DCLPlayerPrefs.GetInt(GetCurrentChannelSettingsKey(DCLPrefKeys.SETTINGS_CHAT_SOUNDS, channelId), defaultNotificationPingValue);
        }

        /// <summary>
        /// Replaces the selected option for the Notification ping sound behaviour, for a given chat conversation.
        /// </summary>
        /// <param name="selectedMode">The new value.</param>
        /// <param name="channelId">The id of the conversation.</param>
        public static void SetNotificationPintValuePerChannel(ChatAudioSettings selectedMode, ChatChannel.ChannelId channelId)
        {
            DCLPlayerPrefs.SetInt(GetCurrentChannelSettingsKey(DCLPrefKeys.SETTINGS_CHAT_SOUNDS, channelId), (int)selectedMode, save: true);
        }

        // Decorates a property key so it can be properly stored/loaded in player prefs for the current user and channel
        private static string GetCurrentChannelSettingsKey(string propertyKey, ChatChannel.ChannelId currentChannelId)
        {
            // TODO: The player prefs class should provide a way to save date per user in the same computer. Otherwise, the Id of the user will have to be concatenated to the key
            return currentChannelId.Id + propertyKey;
        }
    }
}
