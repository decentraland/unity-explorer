using System;

namespace DCL.Friends.Chat
{
    public interface IChatLifecycleBusController
    {
        void ShowChat();
        void HideChat();

        void SubscribeToHideChatCommand(Action action);
    }
}
