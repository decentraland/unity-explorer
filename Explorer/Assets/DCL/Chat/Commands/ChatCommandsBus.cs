using System;

namespace DCL.Chat.Commands
{

    /// <summary>
    ///     This bus is used by chat commands, to avoid sending references to classes they should not have, like specific controllers or views
    ///     Just send this interface to both ends, subscribe to the event in the controller/etc. and wait for the event to be raised. Just like magic.
    /// </summary>
    public interface IChatCommandsBus
    {
        event Action<bool> OnSetConnectionStatusPanelVisibility;
        event Action OnClearChat;

        void SetConnectionStatusPanelVisibility(bool isVisible);

        void ClearChat();
    }

    public class ChatCommandsBus : IChatCommandsBus
    {
        public event Action<bool> OnSetConnectionStatusPanelVisibility;
        public event Action OnClearChat;

        public void SetConnectionStatusPanelVisibility(bool isVisible)
        {
            OnSetConnectionStatusPanelVisibility?.Invoke(isVisible);
        }

        public void ClearChat()
        {
            OnClearChat?.Invoke();
        }
    }
}
