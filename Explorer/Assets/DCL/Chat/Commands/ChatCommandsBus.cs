using System;

namespace DCL.Chat.Commands
{
    public interface IChatCommandsBus
    {
        event Action<bool> OnSetConnectionStatusPanelVisibility;

        void SetConnectionStatusPanelVisibility(bool isVisible);
    }

    public class ChatCommandsBus : IChatCommandsBus
    {
        public event Action<bool> OnSetConnectionStatusPanelVisibility;

        public void SetConnectionStatusPanelVisibility(bool isVisible)
        {
            OnSetConnectionStatusPanelVisibility?.Invoke(isVisible);
        }
    }
}
