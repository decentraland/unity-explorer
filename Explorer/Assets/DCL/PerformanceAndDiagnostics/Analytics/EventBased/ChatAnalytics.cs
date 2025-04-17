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
        }

        public void Dispose()
        {
        }
    }
}
