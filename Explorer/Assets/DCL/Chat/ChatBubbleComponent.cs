namespace DCL.Chat
{
    public struct ChatBubbleComponent
    {
        public readonly string ChatMessage;
        public readonly string SenderName;
        public readonly string SenderId;
        public readonly bool IsMention;
        public readonly bool IsPrivateMessage;

        public ChatBubbleComponent(string chatMessage, string senderName, string senderId, bool isMention, bool isPrivateMessage)
        {
            ChatMessage = chatMessage;
            SenderName = senderName;
            SenderId = senderId;
            IsMention = isMention;
            IsPrivateMessage = isPrivateMessage;
        }
    }
}
