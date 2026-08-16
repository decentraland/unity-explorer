using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Profiles;
using DCL.Profiles.Self;
using Newtonsoft.Json.Linq;
using System;
using System.Text.RegularExpressions;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class ChatMessagesBusAnalyticsDecorator : IChatMessagesBus
    {
        private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)@([A-Za-z0-9]{1,15}(?:#[A-Za-z0-9]{4})?)(?=\s|!|\?|\.|,|$)", RegexOptions.Compiled);

        private readonly IChatMessagesBus core;
        private readonly IAnalyticsController analytics;
        private readonly IProfileCache profileCache;
        private readonly SelfProfile selfProfile;

        public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage>? MessageAdded;

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

        public void Send(ChatChannel channel, string message, ChatMessageOrigin origin, double timestamp)
        {
            core.Send(channel, message, origin, timestamp);

            // Local per-send array: the tracked JObject takes ownership of it, and queued events
            // must keep their mentions untouched by later sends
            JArray mentionWalletIds = new ();
            bool isMentionMessage = CheckIfIsMention(message, mentionWalletIds);

            JObject jsonObject = new JObject
                {
                    { "is_command", message[0] == '/' },
                    { "length", message.Length },
                    { "origin", origin.ToStringValue() },
                    { "is_mention", isMentionMessage},
                    { "mentions", mentionWalletIds },
                    { "is_private", channel.ChannelType == ChatChannel.ChatChannelType.USER},
                    // { "emoji_count", emoji_count },
                };

            if (timestamp > 0 && selfProfile is { OwnProfile: not null })
                jsonObject.Add("message_id", ChatUtils.GetId(selfProfile.OwnProfile.UserId, timestamp));

            if (channel.ChannelType == ChatChannel.ChatChannelType.USER)
                jsonObject.Add("receiver_id", channel.Id.Id);

            if (channel.ChannelType == ChatChannel.ChatChannelType.COMMUNITY)
                jsonObject.Add("community_id", ChatChannel.GetCommunityIdFromChannelId(channel.Id));

            analytics.Track(AnalyticsEvents.Ui.MESSAGE_SENT, jsonObject);
        }

        private bool CheckIfIsMention(string message, JArray mentionWalletIds)
        {
            var isValidMention = false;
            var matches = USERNAME_REGEX.Matches(message);

            if (matches.Count == 0)
                return false;

            foreach (Match match in matches)
            {
                //using group 1 to remove the @ symbol
                ProfileTier? profile = profileCache.GetByUserName(match.Groups[1].Value);

                if (profile != null)
                {
                    // JArray.Add takes object, so UserId's implicit string conversion does not apply:
                    // the wire shape carries the wallet-address string, never the domain object
                    mentionWalletIds.Add(profile.Value.UserId.Value);
                    //returning a valid mention only if at least one of the mentions are a real user
                    isValidMention = true;
                }
            }

            return isValidMention;
        }
    }
}

