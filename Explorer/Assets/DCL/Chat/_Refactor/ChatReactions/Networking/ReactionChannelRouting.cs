using DCL.Chat.History;

namespace DCL.Chat.ChatReactions.Networking
{
    public readonly struct ReactionChannelRouting
    {
        public readonly ChatChannel.ChatChannelType ChannelType;
        public readonly string ChannelId;

        public ReactionChannelRouting(ChatChannel.ChatChannelType channelType, string channelId)
        {
            ChannelType = channelType;
            ChannelId = channelId;
        }
    }
}
