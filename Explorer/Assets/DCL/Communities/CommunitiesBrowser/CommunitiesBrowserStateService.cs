using System;
using System.Collections.Generic;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserStateService
    {
        private readonly List<CommunityData> myCommunities = new();
        private readonly List<CommunityData> filteredResults = new();
        private readonly List<CommunityData> browseAllResults = new();
        private readonly List<CommunityData> streamingResults = new();

        public IReadOnlyList<CommunityData> MyCommunities => myCommunities.AsReadOnly();
        public IReadOnlyList<CommunityData> FilteredResults => filteredResults.AsReadOnly();
        public IReadOnlyList<CommunityData> BrowseAllResults => browseAllResults.AsReadOnly();
        public IReadOnlyList<CommunityData> StreamingResults => streamingResults.AsReadOnly();

        public void AddMyCommunities(CommunityData[] communities)
        {
            myCommunities.AddRange(communities);
        }

        public void AddFilteredResults(CommunityData[] communities)
        {
            filteredResults.AddRange(communities);
        }

        public void AddBrowseAllResults(CommunityData[] communities)
        {
            browseAllResults.AddRange(communities);
        }

        public void AddStreamingResults(CommunityData[] communities)
        {
            streamingResults.AddRange(communities);
        }

        public void ClearMyCommunities()
        {
            myCommunities.Clear();
        }

        public void ClearFilteredResults()
        {
            filteredResults.Clear();
        }

        public void ClearBrowseAllResults()
        {
            browseAllResults.Clear();
        }

        public void ClearStreamingResults()
        {
            streamingResults.Clear();
        }

        public CommunityData? GetMyCommunityById(string communityId)
        {
            foreach (CommunityData communityData in myCommunities)
            {
                if (communityData.id == communityId)
                    return communityData;
            }
            return null;
        }

        public CommunityData? GetFilteredResultById(string communityId)
        {
            foreach (CommunityData communityData in filteredResults)
            {
                if (communityData.id == communityId)
                    return communityData;
            }
            return null;
        }

        public CommunityData? GetBrowseAllResultById(string communityId)
        {
            foreach (CommunityData communityData in browseAllResults)
            {
                if (communityData.id == communityId)
                    return communityData;
            }
            return null;
        }

        public CommunityData? GetStreamingResultById(string communityId)
        {
            foreach (CommunityData communityData in streamingResults)
            {
                if (communityData.id == communityId)
                    return communityData;
            }
            return null;
        }

        public void UpdateJoinedCommunity(string communityId, bool isJoined, bool isSuccess)
        {
            if (!isSuccess) return;

            // Find the community in all lists
            CommunityData? myCommunityData = GetMyCommunityById(communityId);
            CommunityData? filteredResultData = GetFilteredResultById(communityId);
            CommunityData? browseAllResultData = GetBrowseAllResultById(communityId);
            CommunityData? streamingResultData = GetStreamingResultById(communityId);

            // Update all instances, avoiding duplicate updates of the same reference
            if (myCommunityData != null)
                myCommunityData.SetAsJoined(isJoined);

            if (filteredResultData != null && !ReferenceEquals(filteredResultData, myCommunityData))
                filteredResultData.SetAsJoined(isJoined);

            if (browseAllResultData != null && !ReferenceEquals(browseAllResultData, myCommunityData) && !ReferenceEquals(browseAllResultData, filteredResultData))
                browseAllResultData.SetAsJoined(isJoined);

            if (streamingResultData != null && !ReferenceEquals(streamingResultData, myCommunityData) && !ReferenceEquals(streamingResultData, filteredResultData) && !ReferenceEquals(streamingResultData, browseAllResultData))
                streamingResultData.SetAsJoined(isJoined);

            // Handle My Communities list updates
            if (isJoined)
            {
                // Add to My Communities if not already there
                if (myCommunityData == null)
                {
                    // Find the best source data to add
                    CommunityData? sourceData = filteredResultData ?? browseAllResultData ?? streamingResultData;
                    if (sourceData != null)
                    {
                        myCommunities.Add(sourceData);
                    }
                }
            }
            else
            {
                // Remove from My Communities
                if (myCommunityData != null)
                {
                    myCommunities.RemoveAll(c => c.id == communityId);
                }
            }
        }

        public void RemoveOneMemberFromCounter(string communityId)
        {
            // Find the community in all lists
            CommunityData? myCommunityData = GetMyCommunityById(communityId);
            CommunityData? filteredResultData = GetFilteredResultById(communityId);
            CommunityData? browseAllResultData = GetBrowseAllResultById(communityId);
            CommunityData? streamingResultData = GetStreamingResultById(communityId);

            // Update all instances, avoiding duplicate updates of the same reference
            if (myCommunityData != null)
                myCommunityData.DecreaseMembersCount();

            if (filteredResultData != null && !ReferenceEquals(filteredResultData, myCommunityData))
                filteredResultData.DecreaseMembersCount();

            if (browseAllResultData != null && !ReferenceEquals(browseAllResultData, myCommunityData) && !ReferenceEquals(browseAllResultData, filteredResultData))
                browseAllResultData.DecreaseMembersCount();

            if (streamingResultData != null && !ReferenceEquals(streamingResultData, myCommunityData) && !ReferenceEquals(streamingResultData, filteredResultData) && !ReferenceEquals(streamingResultData, browseAllResultData))
                streamingResultData.DecreaseMembersCount();
        }

        public int GetMyCommunitiesCount() => myCommunities.Count;
        public int GetFilteredResultsCount() => filteredResults.Count;
        public int GetBrowseAllResultsCount() => browseAllResults.Count;
        public int GetStreamingResultsCount() => streamingResults.Count;
    }
}
