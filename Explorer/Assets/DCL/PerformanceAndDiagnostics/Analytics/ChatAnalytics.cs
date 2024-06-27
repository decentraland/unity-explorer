using DCL.Chat;
using Segment.Serialization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class ChatAnalytics
    {
        private static readonly Regex EMOJI_PATTERN = new (@"(\p{Cs}|\p{So})", RegexOptions.Compiled);

        private readonly AnalyticsController analytics;
        private readonly ChatController chatController;
        private readonly IChatMessagesBus chatMessagesBus;

        public ChatAnalytics(AnalyticsController analytics, ChatController chatController, IChatMessagesBus chatMessagesBus)
        {
            this.analytics = analytics;
            this.chatController = chatController;
            this.chatMessagesBus = chatMessagesBus;

            chatController.ChatBubbleVisibilityChanged += OnChatBubbleVisibilityChanged;
            chatMessagesBus.MessageSent += OnMessageSent;
        }

        ~ChatAnalytics()
        {
            chatController.ChatBubbleVisibilityChanged -= OnChatBubbleVisibilityChanged;
            chatMessagesBus.MessageSent -= OnMessageSent;
        }

        private void OnMessageSent(string message)
        {
            var emojisAmount = EMOJI_PATTERN.Matches(message).Count;
            analytics.Track("chat_message_sent", new Dictionary<string, JsonElement>
            {
                { "message", message },
                { "emojis_amount", EMOJI_PATTERN.Matches(message).Count },
            });
        }

        private void OnChatBubbleVisibilityChanged(bool isVisible)
        {
            analytics.Track("chat_bubble_switched", new Dictionary<string, JsonElement>
            {
                { "is_visible", isVisible },
            });
        }
    }
}
