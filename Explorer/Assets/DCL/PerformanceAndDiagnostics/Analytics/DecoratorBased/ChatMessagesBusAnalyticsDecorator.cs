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

        public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage> MessageAdded;

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

        private void ReEmit(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, ChatMessage obj) =>
            MessageAdded?.Invoke(channelId, channelType, obj);

        public void Send(ChatChannel channel, string message, string origin, string topic)
        {
            core.Send(channel, message, origin, topic);

            JsonObject jsonObject = new JsonObject
                {
                    { "is_command", message[0] == '/' },
                    { "origin", origin },
                    { "is_mention", CheckIfIsMention(message)},
                    { "is_private", channel.ChannelType == ChatChannel.ChatChannelType.USER},
                    // TODO: Add community id

                    //TODO FRAN: Add here array of mentioned players.
                    // { "emoji_count", emoji_count },
                };

            if (channel.ChannelType == ChatChannel.ChatChannelType.USER)
                jsonObject.Add("receiver_id", channel.Id.Id);

            analytics.Track(AnalyticsEvents.UI.MESSAGE_SENT, jsonObject);
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
