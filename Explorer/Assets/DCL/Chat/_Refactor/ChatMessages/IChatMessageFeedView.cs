using System.Collections.Generic;
using DG.Tweening;

namespace DCL.Chat
{
    public interface IChatMessageFeedView
    {
        void SetMessages(IReadOnlyList<MessageData> messages);

        void Clear();
        // Add other methods as needed, e.g., ScrollToBottom()
        
        void Show();
        void Hide();
        void SetFocusedState(bool isFocused, bool animate, float duration, Ease easing);
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