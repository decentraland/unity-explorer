using DCL.Chat;
using DCL.Chat.MessageBus;
using Segment.Serialization;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class ChatAnalytics: IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly ChatController chatController;
        private readonly IChatMessagesBus chatMessagesBus;

        private bool isInitChatBubble = true;

        public ChatAnalytics(IAnalyticsController analytics, ChatController chatController, IChatMessagesBus chatMessagesBus)
        {
            this.analytics = analytics;
            this.chatController = chatController;
            this.chatMessagesBus = chatMessagesBus;

            chatController.ChatBubbleVisibilityChanged += OnChatBubbleVisibilityChanged;
            this.chatMessagesBus.MessageSent += OnMessageSent;
        }

        public void Dispose()
        {
            chatController.ChatBubbleVisibilityChanged -= OnChatBubbleVisibilityChanged;
            chatMessagesBus.MessageSent -= OnMessageSent;
        }

        private void OnMessageSent(string message)
        {
            analytics.Track(AnalyticsEvents.Chat.MESSAGE_SENT, new JsonObject
            {
                { "message", message },
            });
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
