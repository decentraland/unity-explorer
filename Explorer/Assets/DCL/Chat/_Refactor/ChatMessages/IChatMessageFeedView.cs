using System.Collections.Generic;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DG.Tweening;

namespace DCL.Chat
{
    public interface IChatMessageFeedView
    {
        // Event to notify the presenter that the user has reached the bottom
        event System.Action OnScrollToBottom;

        // Replaces all messages in the view. Used when changing channels.
        void SetMessages(IReadOnlyList<ChatMessageViewModel> messages);
        
        void AppendMessage(ChatMessageViewModel message, bool animated);

        // Scrolls the view to the very last message.
        void ScrollToBottom();

        // Let the presenter know if the view is at the bottom
        bool IsAtBottom();

        void Clear();
        void Show();
        void Hide();
        void SetFocusedState(bool isFocused, bool animate, float duration, Ease easing);
    }
}