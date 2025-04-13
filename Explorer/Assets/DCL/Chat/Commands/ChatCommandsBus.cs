using System;

namespace DCL.Chat.Commands
{
    /// <summary>
    ///     This bus is used by chat commands, to avoid sending references to classes they should not have, like specific controllers or views
    ///     Just send this interface to both ends, subscribe to the event in the controller/etc. and wait for the event to be raised. Just like magic.
    /// </summary>
    public interface IChatCommandsBus
    {
        event Action<bool> SetConnectionStatusPanelVisibility;
        event Action ClearChat;

        void OnSetConnectionStatusPanelVisibility(bool isVisible);

        void OnClearChat();
    }

    public class ChatCommandsBus : IChatCommandsBus
    {
        public event Action<bool> SetConnectionStatusPanelVisibility;
        public event Action ClearChat;

        public void OnSetConnectionStatusPanelVisibility(bool isVisible)
        {
            SetConnectionStatusPanelVisibility?.Invoke(isVisible);
        }

        public void OnClearChat()
        {
            ClearChat?.Invoke();
        }
    }
}
