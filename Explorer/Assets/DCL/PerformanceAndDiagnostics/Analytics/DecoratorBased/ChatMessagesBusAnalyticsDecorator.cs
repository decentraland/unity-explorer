using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Profiles;
using DCL.Profiles.Self;
using Segment.Serialization;
using System;
using System.Text.RegularExpressions;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class ChatMessagesBusAnalyticsDecorator : IChatMessagesBus
    {
        private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)@([A-Za-z0-9]{3,15}(?:#[A-Za-z0-9]{4})?)(?=\s|!|\?|\.|,|$)", RegexOptions.Compiled);

        private readonly IChatMessagesBus core;
        private readonly IAnalyticsController analytics;
        private readonly IProfileCache profileCache;
        private readonly SelfProfile selfProfile;

        public event Action<ChatChannel.ChannelId, ChatMessage> MessageAdded;

        public ChatMessagesBusAnalyticsDecorator(IChatMessagesBus core, IAnalyticsController analytics, IProfileCache profileCache, SelfProfile selfProfile)
        {
            this.core = core;
            this.analytics = analytics;
            this.profileCache = profileCache;
            this.selfProfile = selfProfile;

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
                { "is_mention", CheckIfIsMention(message)}

                // { "emoji_count", emoji_count },
                // { "message", message },
                // { "channel_mame", "nearby"}, // temporally hardcoded
                // { "receiver_id", string.Empty} // temporal mock
            });
        }

        private bool  CheckIfIsMention(string message)
        {
            var matches = USERNAME_REGEX.Matches(message);

            foreach (Match match in matches)
            {
                if (match.Value == selfProfile.OwnProfile?.DisplayName)
                    return true;

                if (profileCache.GetByUserName(match.Value) != null)
                    return true;
            }
            return false;
        }
    }
}
