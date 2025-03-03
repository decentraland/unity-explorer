using System;

namespace DCL.Chat.ChatLifecycleBus
{
    public interface IChatLifecycleBusController
    {
        void ShowChat();
        void HideChat();

        void SubscribeToHideChatCommand(Action action);
    }
}
