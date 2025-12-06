using DCL.ChatArea;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class ChatEventsAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly ChatMainSharedAreaController chatController;

        public ChatEventsAnalytics(IAnalyticsController analytics, ChatMainSharedAreaController chatController)
        {
            this.analytics = analytics;
            this.chatController = chatController;

            chatController.CommandRegistry.SelectChannel.ChannelOpened += OnConversationOpened;
            chatController.CommandRegistry.CloseChannel.ChannelClosed += OnConversationClosed;
        }

        public void Dispose()
        {
            chatController.CommandRegistry.SelectChannel.ChannelOpened -= OnConversationOpened;
            chatController.CommandRegistry.CloseChannel.ChannelClosed -= OnConversationClosed;
        }

        private void OnConversationClosed() =>
            analytics.Track(AnalyticsEvents.UI.CHAT_CONVERSATION_CLOSED);

        private void OnConversationOpened(bool wasAlreadyOpen) =>
            analytics.Track(AnalyticsEvents.UI.CHAT_CONVERSATION_OPENED, new JObject
            {
                { "was_already_open", wasAlreadyOpen },
            });
    }
}
