using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.Communities
{
    public class FakeCommunitiesDataProvider : ICommunitiesDataProvider
    {
        private bool shouldReturnEmpty = false;

        public FakeCommunitiesDataProvider(IWebRequestController webRequestController, IWeb3IdentityCache web3IdentityCache, IDecentralandUrlsSource urlsSource)
        {

        }

        public async UniTask<GetCommunityResponse> GetCommunityAsync(string communityId, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetUserCommunitiesResponse> GetUserCommunitiesAsync(string userId, bool isOwner, bool isMember, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            List<GetUserCommunitiesResponse.CommunityData> filteredCommunities = GetFakeCommunitiesForBrowserTesting(2, 13)
                                                                                .Where(x => (isOwner && x.role == CommunityMemberRole.Owner) || (isMember && x.role == CommunityMemberRole.Member))
                                                                                .ToList();

            var totalPages = (int)Math.Ceiling((double)filteredCommunities.Count / elementsPerPage);

            List<GetUserCommunitiesResponse.CommunityData> paginatedCommunities = new();
            for (var i = 0; i < filteredCommunities.Count; i++)
            {
                if (i >= (pageNumber - 1) * elementsPerPage && i < pageNumber * elementsPerPage)
                    paginatedCommunities.Add(filteredCommunities[i]);
            }

            GetUserCommunitiesResponse result = new GetUserCommunitiesResponse
            {
                communities = paginatedCommunities.ToArray(),
                totalPages = totalPages,
            };

            await UniTask.Delay(UnityEngine.Random.Range(1000, 3000), cancellationToken: ct);

            return result;
        }

        public async UniTask<GetUserLandsResponse> GetUserLandsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetUserWorldsResponse> GetUserWorldsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunityAsync(string communityId, string name, string description, byte[] thumbnail, List<Vector2Int> lands,
            List<string> worlds, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetCommunityMembersResponse> GetCommunityMembersAsync(string communityId, bool areBanned, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetUserCommunitiesCompactResponse> GetUserCommunitiesCompactAsync(CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetOnlineCommunityMembersResponse> GetOnlineCommunityMembersAsync(CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<bool> KickUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<bool> BanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<bool> LeaveCommunityAsync(string communityId, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<bool> JoinCommunityAsync(string communityId, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<bool> DeleteCommunityAsync(string communityId, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<bool> SetMemberRoleAsync(string userId, string communityId, CancellationToken ct) =>
            throw new NotImplementedException();

        private List<GetUserCommunitiesResponse.CommunityData> GetFakeCommunitiesForBrowserTesting(int communitiesAsOwner, int communitiesAsMember)
        {
            List<GetUserCommunitiesResponse.CommunityData> communities = new List<GetUserCommunitiesResponse.CommunityData>();

            if (!shouldReturnEmpty)
            {
                for (var i = 0; i < 100; i++)
                {
                    communities.Add(new GetUserCommunitiesResponse.CommunityData
                    {
                        id = (i + 1).ToString(),
                        thumbnails = new[] { "https://picsum.photos/128/128" },
                        name = $"Community {i + 1}",
                        description = $"Test description for Community {i + 1}",
                        ownerId = string.Empty,
                        privacy = CommunityPrivacy.@public,
                        role = i < communitiesAsOwner ? CommunityMemberRole.Owner :
                            i < communitiesAsOwner + communitiesAsMember ? CommunityMemberRole.Member : CommunityMemberRole.None,
                    });
                }
            }

            shouldReturnEmpty = !shouldReturnEmpty;
            return communities;
        }
    }
}
