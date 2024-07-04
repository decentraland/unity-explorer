using DCL.Chat;
using Segment.Serialization;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class ChatEventsAnalytics: IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly ChatController chatController;

        private bool isInitChatBubble = true;

        public ChatEventsAnalytics(IAnalyticsController analytics, ChatController chatController)
        {
            this.analytics = analytics;
            this.chatController = chatController;

            chatController.ChatBubbleVisibilityChanged += OnChatBubbleVisibilityChanged;
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

            analytics.Track(AnalyticsEvents.Chat.BUBBLE_SWITCHED, new JsonObject
            {
                { "is_visible", isVisible },
            });
        }
    }
}
