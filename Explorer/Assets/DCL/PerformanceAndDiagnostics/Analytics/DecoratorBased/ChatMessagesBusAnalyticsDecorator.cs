using DCL.Chat;
using DCL.Chat.MessageBus;
using Segment.Serialization;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class ChatMessagesBusAnalyticsDecorator : IChatMessagesBus
    {
        private readonly IChatMessagesBus core;
        private readonly IAnalyticsController analytics;

        public event Action<ChatMessage> MessageAdded;

        public ChatMessagesBusAnalyticsDecorator(IChatMessagesBus core, IAnalyticsController analytics)
        {
            this.core = core;
            this.analytics = analytics;

            core.MessageAdded += OnMessageAdded;
        }

        public void Dispose()
        {
            core.MessageAdded -= OnMessageAdded;
        }

        private void OnMessageAdded(ChatMessage obj) =>
            MessageAdded?.Invoke(obj);

        public void Send(string message)
        {
            core.Send(message);

            analytics.Track(AnalyticsEvents.Chat.MESSAGE_SENT, new JsonObject
            {
                { "message", message },
            });
        }
    }
}
