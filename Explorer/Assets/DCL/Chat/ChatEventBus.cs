using System;

namespace DCL.Chat.EventBus
{
    public class ChatEventBus : IChatEventBus
    {
        public event IChatEventBus.InsertTextInChatDelegate? InsertTextInChat;
        public event IChatEventBus.InsertSystemMessageInChatDelegate? InsertSystemMessageInChat;
        public event IChatEventBus.OpenConversationDelegate? OpenConversation;

        public void InsertText(string text)
        {
            InsertTextInChat?.Invoke(text);
        }
        
        public void InsertSystemMessage(string message)
        {
            InsertSystemMessageInChat?.Invoke(message);
        }

        public void OpenConversationUsingUserId(string userId)
        {
            OpenConversation?.Invoke(userId);
        }
    }
}
