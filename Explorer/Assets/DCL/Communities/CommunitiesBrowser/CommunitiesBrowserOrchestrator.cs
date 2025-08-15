using DCL.Utilities;
using System;
using System.Collections.Generic;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public enum BrowserSectionType
    {
        BROWSE_ALL_SECTION,
        FILTERED_RESULTS_SECTION,
        UNDEFINED,
    }

    public class CommunitiesBrowserOrchestrator
    {
        public const int COMMUNITIES_PER_PAGE = 20;

        private readonly Dictionary<string, CommunityData> filteredCommunitiesById = new();
        private readonly List<CommunityData> filteredCommunities = new ();
        private readonly Dictionary<string, CommunityData> myCommunitiesById = new();
        private readonly List<CommunityData> myCommunities = new ();
        private readonly ReactiveProperty<BrowserSectionType> browserSectionType = new (BrowserSectionType.BROWSE_ALL_SECTION);

        public event Action<string>? CommunityRefreshRequested;
        public event Action<string>? CommunityJoined;
        public event Action<string>? CommunityProfileOpened;

        public event Action? OnResultCommunitiesChanged;
        public event Action? OnMyCommunitiesChanged;

        public string currentNameFilter { get; private set; }
        public int currentPageNumberFilter = 1;
        public int currentResultsTotalAmount;
        public bool currentOnlyMemberOf;
        public bool isGridResultsLoadingItems;

        public IReadonlyReactiveProperty<BrowserSectionType> CurrentBrowserSectionType => browserSectionType;

        public void RequestCommunityRefresh(string communityId)
        {
            CommunityRefreshRequested?.Invoke(communityId);
        }

        public void RequestJoinCommunity(string communityId)
        {
            CommunityJoined?.Invoke(communityId);
        }

        public void RequestOpenCommunityProfile(string communityId)
        {
            CommunityProfileOpened?.Invoke(communityId);
        }

        // State management methods
        public void UpdateCommunities(IEnumerable<CommunityData> newCommunities)
        {
            filteredCommunitiesById.Clear();
            foreach (var community in newCommunities)
            {
                filteredCommunitiesById[community.id] = community;
            }
            OnResultCommunitiesChanged?.Invoke();
        }

        public void UpdateMyCommunities(IEnumerable<CommunityData> newCommunities)
        {
            myCommunitiesById.Clear();
            foreach (var community in newCommunities)
            {
                myCommunitiesById[community.id] = community;
                myCommunities.Add(community);
            }
            OnMyCommunitiesChanged?.Invoke();
        }

        public void AddCommunity(CommunityData community)
        {
            filteredCommunitiesById[community.id] = community;
            filteredCommunities.Add(community);
            OnResultCommunitiesChanged?.Invoke();
        }

        public void UpdateCommunity(string communityId, CommunityData community)
        {
            if (filteredCommunitiesById.ContainsKey(communityId))
            {
                filteredCommunitiesById[communityId] = community;
                OnResultCommunitiesChanged?.Invoke();
            }

            if (myCommunitiesById.ContainsKey(communityId))
            {
                myCommunitiesById[communityId] = community;
                OnMyCommunitiesChanged?.Invoke();
            }
        }

        public void RemoveCommunity(string communityId)
        {
            if (filteredCommunitiesById.Remove(communityId))
                OnResultCommunitiesChanged?.Invoke();

            if (myCommunitiesById.Remove(communityId))
                OnMyCommunitiesChanged?.Invoke();
        }

        public void ClearMyCommunities()
        {
            myCommunitiesById.Clear();
            myCommunities.Clear();
            OnMyCommunitiesChanged?.Invoke();
        }

        public void ClearResults()
        {
            filteredCommunitiesById.Clear();
            OnResultCommunitiesChanged?.Invoke();
        }

        public void AddMyCommunity(CommunityData community)
        {
            myCommunitiesById[community.id] = community;
            myCommunities.Add(community);
            OnMyCommunitiesChanged?.Invoke();
        }

        public void AddResultCommunity(CommunityData community)
        {
            filteredCommunitiesById[community.id] = community;
            OnResultCommunitiesChanged?.Invoke();
        }

        public void UpdateJoinedCommunity(string communityId, bool isJoined)
        {
            if (filteredCommunitiesById.TryGetValue(communityId, out var resultCommunity))
            {
                resultCommunity.SetAsJoined(isJoined);
                OnResultCommunitiesChanged?.Invoke();
            }

            if (myCommunitiesById.TryGetValue(communityId, out var myCommunity))
            {
                //Since we are updating currentMyCommunities with the resultCommunityData, we need to check if they are the same instance
                //so we avoid updating the same instance twice
                if (!ReferenceEquals(myCommunity, resultCommunity))
                {
                    myCommunity?.SetAsJoined(isJoined);
                    OnMyCommunitiesChanged?.Invoke();
                }
            }

            // Add/remove the joined/left community to/from My Communities
            if (resultCommunity != null && isJoined)
                myCommunitiesById[communityId] = resultCommunity;
            else if (myCommunity != null)
                myCommunitiesById.Remove(communityId);

        }

        public void DecreaseMembersCount(string communityId)
        {
            if (filteredCommunitiesById.TryGetValue(communityId, out var resultCommunity))
            {
                resultCommunity.DecreaseMembersCount();
                OnResultCommunitiesChanged?.Invoke();
            }

            if (myCommunitiesById.TryGetValue(communityId, out var myCommunity))
            {
                //Since we are updating currentMyCommunities with the resultCommunityData, we need to check if they are the same instance
                //so we avoid updating the same instance twice
                if (!ReferenceEquals(myCommunity, resultCommunity))
                {
                    myCommunity.DecreaseMembersCount();
                    OnMyCommunitiesChanged?.Invoke();
                }
            }
        }

        public IReadOnlyDictionary<string, CommunityData> GetFilteredCommunitiesById() => filteredCommunitiesById;
        public IReadOnlyDictionary<string, CommunityData> GetMyCommunitiesById() => myCommunitiesById;
        public IReadOnlyList<CommunityData> GetFilteredCommunities() => filteredCommunities;
        public IReadOnlyList<CommunityData> GetMyCommunities() => myCommunities;


        public CommunityData? GetFilteredCommunity(string communityId)
        {
            filteredCommunitiesById.TryGetValue(communityId, out var community);
            return community;
        }

        public CommunityData? GetMyCommunity(string communityId)
        {
            myCommunitiesById.TryGetValue(communityId, out var community);
            return community;
        }

        public int GetCommunitiesCount() => filteredCommunitiesById.Count;
        public int GetMyCommunitiesCount() => myCommunitiesById.Count;

        public bool HasCommunities() => filteredCommunitiesById.Count > 0;
        public bool HasMyCommunities() => myCommunitiesById.Count > 0;
    }
}
