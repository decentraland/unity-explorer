using System.Collections.Generic;

namespace DCL.Chat
{
    public interface IChatMessageFeedView
    {
        void SetMessages(IReadOnlyList<MessageData> messages);

        void Clear();
        // Add other methods as needed, e.g., ScrollToBottom()
    }

    public struct MessageData
    {
        public string Body;
        public string SenderName;
        public string SenderWalletAddress;
        public string SenderImageUrl;

        public bool IsOwnMessage;
        // etc.
    }
}