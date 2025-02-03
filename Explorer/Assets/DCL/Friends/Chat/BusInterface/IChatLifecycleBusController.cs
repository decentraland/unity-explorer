using System;

namespace DCL.Friends.Chat.BusInterface
{
    public interface IChatLifecycleBusController
    {
        void ShowChat();
        void HideChat();

        void SubscribeToHideChatCommand(Action action);
    }
}
