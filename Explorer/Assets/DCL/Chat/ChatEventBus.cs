
namespace DCL.Chat.EventBus
{
    public class ChatEventBus : IChatEventBus
    {
        public event IChatEventBus.InsertTextInChatRequestedDelegate? InsertTextInChatRequested;
        public event IChatEventBus.OpenPrivateConversationRequestedDelegate? OpenPrivateConversationRequested;
        public event IChatEventBus.OpenCommunityConversationRequestedDelegate? OpenCommunityConversationRequested;

        public void InsertText(string text)
        {
            InsertTextInChatRequested?.Invoke(text);
        }

        public void OpenPrivateConversationUsingUserId(string userId)
        {
            OpenPrivateConversationRequested?.Invoke(userId);
        }

        public void OpenCommunityConversationUsingUserId(string communityId)
        {
            OpenCommunityConversationRequested?.Invoke(communityId);
        }
    }
}
