using DCL.Chat;
using System;

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
            if (isVisible) return;

            analytics.Track("chat_bubble_turned_off");
        }
    }
}
