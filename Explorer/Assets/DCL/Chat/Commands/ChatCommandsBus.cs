using System;

namespace DCL.Chat.Commands
{
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
