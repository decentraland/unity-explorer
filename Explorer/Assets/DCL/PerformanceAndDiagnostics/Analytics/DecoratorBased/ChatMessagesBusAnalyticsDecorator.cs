using DCL.Chat.History;
using DCL.Chat.MessageBus;
using Segment.Serialization;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class ChatMessagesBusAnalyticsDecorator : IChatMessagesBus
    {
        private readonly IChatMessagesBus core;
        private readonly IAnalyticsController analytics;

        public event Action<ChatChannel.ChannelId, ChatMessage> MessageAdded;

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

        private void ReEmit(ChatChannel.ChannelId channelId, ChatMessage obj) =>
            MessageAdded?.Invoke(channelId, obj);

        public void Send(ChatChannel.ChannelId channelId, string message, string origin)
        {
            core.Send(channelId, message, origin);

            analytics.Track(AnalyticsEvents.UI.MESSAGE_SENT, new JsonObject
            {
                { "is_command", message[0] == '/' },
                { "origin", origin },

                // { "emoji_count", emoji_count },
                // { "message", message },
                // { "channel_mame", "nearby"}, // temporally hardcoded
                // { "receiver_id", string.Empty} // temporal mock
            });
        }
    }
}
