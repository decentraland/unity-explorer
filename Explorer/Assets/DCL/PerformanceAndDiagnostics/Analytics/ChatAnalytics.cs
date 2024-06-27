using DCL.Chat;
using Segment.Serialization;
using System;
using System.Collections.Generic;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class ChatAnalytics : IDisposable
    {
        private readonly AnalyticsController analytics;
        private readonly ChatController chatController;

        public ChatAnalytics(AnalyticsController analytics, ChatController chatController)
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
            analytics.Track("chat_bubble_switched", new Dictionary<string, JsonElement>
            {
                { "is_visible", isVisible },
            });
        }
    }
}
