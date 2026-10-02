using DCL.Settings.Settings;
using System;

namespace DCL.Settings
{
    /// <summary>
    /// Allows the settings controller to listen to settings modules's (toggles, dropdowns, buttons...) events by injecting into them when
    /// they are created, without exposing all the methods to those classes.
    /// </summary>
    public interface ISettingsModuleEventListener
    {
        /// <summary>
        /// Raised when the chat bubbles visibility setting is changed in the UI.
        /// </summary>
        event Action<ChatBubbleVisibilitySettings> ChatBubblesVisibilityChanged;

        /// <summary>
        /// Tells the listener to raise the event.
        /// </summary>
        /// <param name="newVisibility">The new value for the visibility of the chat bubbles.</param>
        void NotifyChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings newVisibility);

        /// <summary>
        /// Raised when the lobby toggle is changed in the UI, with the value it had before and the one it has now.
        /// </summary>
        event Action<bool, bool> LobbyEnabledChanged;

        /// <summary>
        /// Tells the listener to raise the event.
        /// </summary>
        /// <param name="previousValue">Whether the lobby was enabled before the change.</param>
        /// <param name="newValue">Whether the lobby is enabled after the change.</param>
        void NotifyLobbyEnabledChanged(bool previousValue, bool newValue);
    }
}
