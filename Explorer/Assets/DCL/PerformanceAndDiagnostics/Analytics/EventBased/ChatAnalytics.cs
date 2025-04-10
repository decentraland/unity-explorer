using DCL.Chat;
using Segment.Serialization;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class ChatEventsAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly ChatController chatController;

        private bool isInitChatBubble = true;

        public ChatEventsAnalytics(IAnalyticsController analytics, ChatController chatController)
        {
            this.analytics = analytics;
            this.chatController = chatController;

            chatController.ChatBubbleVisibilityChanged += OnChatBubbleVisibilityChanged;
            chatController.ConversationOpened += OnConversationOpened;
            chatController.ConversationClosed += OnConversationClosed;
        }

        public void Dispose()
        {
            chatController.ChatBubbleVisibilityChanged -= OnChatBubbleVisibilityChanged;
        }

        private void OnChatBubbleVisibilityChanged(bool isVisible)
        {
            // Skip initialization setup of chat bubble visibility
            if (isInitChatBubble)
            {
                isInitChatBubble = false;
                return;
            }

            analytics.Track(AnalyticsEvents.UI.BUBBLE_SWITCHED, new JsonObject
            {
                { "is_visible", isVisible },
            });
        }

        private void OnConversationClosed()
        {
            analytics.Track(AnalyticsEvents.UI.CHAT_CONVERSATION_CLOSED);
        }

        private void OnConversationOpened(bool wasAlreadyOpen)
        {
            analytics.Track(AnalyticsEvents.UI.CHAT_CONVERSATION_OPENED, new JsonObject
            {
                { "was_already_open", wasAlreadyOpen },
            });
        }
    }
}
