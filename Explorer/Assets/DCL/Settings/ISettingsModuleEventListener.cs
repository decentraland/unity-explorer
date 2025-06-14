using DCL.Settings.Settings;
using System;
using UnityEngine;

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

        void NotifyResolutionChange(Resolution newResolution);

        event Action<Resolution> ResolutionChanged;
    }
}
