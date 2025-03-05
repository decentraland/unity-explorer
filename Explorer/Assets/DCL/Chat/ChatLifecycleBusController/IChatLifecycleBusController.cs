using System;

namespace DCL.Chat.ChatLifecycleBus
{
    public interface IChatLifecycleBusController
    {
        event Action? ChatToggleRequested;
        event Action? ChatHideRequested;

        void ShowChat();
        void HideChat();
        void ToggleChat();
    }
}
