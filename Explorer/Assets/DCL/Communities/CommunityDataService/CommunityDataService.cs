using DCL.Chat.History;
using System.Collections.Generic;

namespace DCL.Communities
{
    public interface ICommunityDataService
    {
        void SetCommunities(IEnumerable<GetUserCommunitiesData.CommunityData> communities);
        bool TryGetCommunity(ChatChannel.ChannelId channelId, out GetUserCommunitiesData.CommunityData communityData);
    }

    public class CommunityDataService : ICommunityDataService
    {
        private readonly Dictionary<ChatChannel.ChannelId, GetUserCommunitiesData.CommunityData> communities = new();

        public void SetCommunities(IEnumerable<GetUserCommunitiesData.CommunityData> newCommunities)
        {
            communities.Clear();
            foreach (var community in newCommunities)
            {
                communities[ChatChannel.NewCommunityChannelId(community.id)] = community;
            }
        }

        public bool TryGetCommunity(ChatChannel.ChannelId channelId, out GetUserCommunitiesData.CommunityData communityData)
        {
            return communities.TryGetValue(channelId, out communityData);
        }
    }
}