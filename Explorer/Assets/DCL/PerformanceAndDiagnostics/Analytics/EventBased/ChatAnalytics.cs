﻿using DCL.Chat;
using Segment.Serialization;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class ChatEventsAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly ChatPanelPresenter chatController;

        private bool isInitChatBubble = true;

        public ChatEventsAnalytics(IAnalyticsController analytics, ChatPanelPresenter chatController)
        {
            this.analytics = analytics;
            this.chatController = chatController;
            //TODO: This needs re-implementing
            //chatController.ConversationOpened += OnConversationOpened;
            //chatController.ConversationClosed += OnConversationClosed;
        }

        public void Dispose()
        {
            //chatController.ConversationOpened -= OnConversationOpened;
            //chatController.ConversationClosed -= OnConversationClosed;
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
