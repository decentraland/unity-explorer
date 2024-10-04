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

            core.MessageAdded += ReEmit;
        }

        public void Dispose()
        {
            core.MessageAdded -= ReEmit;
        }

        private void ReEmit(ChatMessage obj) =>
            MessageAdded?.Invoke(obj);

        public void Send(string message, string origin)
        {
            core.Send(message, origin);

            analytics.Track(AnalyticsEvents.UI.MESSAGE_SENT, new JsonObject
            {
                { "is_command", ChatCommandsHandler.StartsLikeCommand(message) },
                { "origin", origin },

                // { "emoji_count", emoji_count },
                // { "message", message },
                // { "channel_mame", "nearby"}, // temporally hardcoded
                // { "receiver_id", string.Empty} // temporal mock
            });
        }
    }
}
