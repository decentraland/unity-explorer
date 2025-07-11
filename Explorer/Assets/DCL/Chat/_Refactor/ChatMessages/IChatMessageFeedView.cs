using System.Collections.Generic;
using DCL.Chat.History;
using DG.Tweening;

namespace DCL.Chat
{
    public interface IChatMessageFeedView
    {
        // Event to notify the presenter that the user has reached the bottom
        event System.Action OnScrollToBottom;

        // Replaces all messages in the view. Used when changing channels.
        void SetMessages(IReadOnlyList<ChatMessage> messages);

        // Appends a single new message to the end of the list.
        void AppendMessage(ChatMessage message, bool animated);

        // Scrolls the view to the very last message.
        void ScrollToBottom();

        // Let the presenter know if the view is at the bottom
        bool IsAtBottom();

        void Clear();
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