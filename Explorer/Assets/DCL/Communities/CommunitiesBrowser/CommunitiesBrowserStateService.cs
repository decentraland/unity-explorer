using DCL.Communities.CommunitiesDataProvider.DTOs;
using System;
using System.Collections.Generic;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserStateService : IDisposable
    {
        private readonly Dictionary<string, CommunityData> allCommunities = new();

        public CommunityData GetCommunityDataById(string communityId) =>
            allCommunities.GetValueOrDefault(communityId);

        public void AddCommunities(CommunityData[] communities)
        {
            foreach (CommunityData community in communities)
                allCommunities[community.id] = community;
        }

        public void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            if (!isSuccess) return;

            if (!allCommunities.TryGetValue(communityId, out CommunityData? community))
                return;

            community.SetAsJoined(isJoined);
        }

        public void RemoveOneMemberFromCounter(string communityId)
        {
            if (allCommunities.TryGetValue(communityId, out CommunityData? community))
            {
                community.DecreaseMembersCount();
            }
        }

        public void Dispose()
        {
            allCommunities.Clear();
        }
    }
}
