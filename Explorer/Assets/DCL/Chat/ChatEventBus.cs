using System;

namespace DCL.Chat.EventBus
{
    public class ChatEventBus : IChatEventBus
    {
        public event IChatEventBus.InsertTextInChatDelegate? InsertTextInChat;
        public event IChatEventBus.OpenConversationDelegate? OpenConversation;

        public void InsertText(string text)
        {
            InsertTextInChat?.Invoke(text);
        }

        public void OpenConversationUsingUserId(string userId)
        {
            OpenConversation?.Invoke(userId);
        }
    }
}
