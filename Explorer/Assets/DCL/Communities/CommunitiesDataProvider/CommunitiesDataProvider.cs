using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Communities
{
    public class CommunitiesDataProvider : ICommunitiesDataProvider
    {
        private readonly ICommunitiesDataProvider fakeDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IDecentralandUrlsSource urlsSource;

        private string communitiesBaseUrl => urlsSource.Url(DecentralandUrl.Communities);

        public CommunitiesDataProvider(
            ICommunitiesDataProvider fakeDataProvider,
            IWebRequestController webRequestController,
            IWeb3IdentityCache web3IdentityCache,
            IDecentralandUrlsSource urlsSource)
        {
            this.fakeDataProvider = fakeDataProvider;
            this.webRequestController = webRequestController;
            this.web3IdentityCache = web3IdentityCache;
            this.urlsSource = urlsSource;
        }

        public async UniTask<GetCommunityResponse> GetCommunityAsync(string communityId, CancellationToken ct)
        {
            string url = $"{communitiesBaseUrl}/communities/{communityId}";

            GetCommunityResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                      .CreateFromJson<GetCommunityResponse>(WRJsonParser.Newtonsoft);
            return response;
        }

        public async UniTask<GetUserCommunitiesResponse> GetUserCommunitiesAsync(string name, bool onlyMemberOf, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/communities?search={name}&onlyMemberOf={onlyMemberOf.ToString().ToLower()}&offset={(pageNumber * elementsPerPage) - elementsPerPage}&limit={elementsPerPage}";

            GetUserCommunitiesResponse creditsProgramProgressResponse = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                                                  .CreateFromJson<GetUserCommunitiesResponse>(WRJsonParser.Newtonsoft);

            return creditsProgramProgressResponse;
        }

        public UniTask<GetUserLandsResponse> GetUserLandsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            fakeDataProvider.GetUserLandsAsync(userId, pageNumber, elementsPerPage, ct);

        public UniTask<GetUserWorldsResponse> GetUserWorldsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            fakeDataProvider.GetUserWorldsAsync(userId, pageNumber, elementsPerPage, ct);

        public UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunityAsync(string communityId, string name, string description, byte[] thumbnail, List<Vector2Int> lands, List<string> worlds, CancellationToken ct) =>
            fakeDataProvider.CreateOrUpdateCommunityAsync(communityId, name, description, thumbnail, lands, worlds, ct);

        public async UniTask<GetCommunityMembersResponse> GetCommunityMembersAsync(string communityId, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            string url = $"{communitiesBaseUrl}/communities/{communityId}/members?offset={(pageNumber * elementsPerPage) - elementsPerPage}&limit={elementsPerPage}";

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                           .CreateFromJson<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft);
            return response;
        }

        public async UniTask<GetCommunityMembersResponse> GetBannedCommunityMembersAsync(string communityId, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            string url = $"{communitiesBaseUrl}/communities/{communityId}/bans?offset={(pageNumber * elementsPerPage) - elementsPerPage}&limit={elementsPerPage}";

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                             .CreateFromJson<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft);
            return response;
        }

        public UniTask<GetUserCommunitiesCompactResponse> GetUserCommunitiesCompactAsync(CancellationToken ct) =>
            fakeDataProvider.GetUserCommunitiesCompactAsync(ct);

        public UniTask<GetOnlineCommunityMembersResponse> GetOnlineCommunityMembersAsync(CancellationToken ct) =>
            fakeDataProvider.GetOnlineCommunityMembersAsync(ct);

        public UniTask<bool> KickUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            fakeDataProvider.KickUserFromCommunityAsync(userId, communityId, ct);

        public UniTask<bool> BanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            fakeDataProvider.BanUserFromCommunityAsync(userId, communityId, ct);

        public UniTask<bool> UnBanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            fakeDataProvider.UnBanUserFromCommunityAsync(userId, communityId, ct);

        public UniTask<bool> LeaveCommunityAsync(string communityId, CancellationToken ct) =>
            fakeDataProvider.LeaveCommunityAsync(communityId, ct);

        public UniTask<bool> JoinCommunityAsync(string communityId, CancellationToken ct) =>
            fakeDataProvider.JoinCommunityAsync(communityId, ct);

        public UniTask<bool> DeleteCommunityAsync(string communityId, CancellationToken ct) =>
            fakeDataProvider.DeleteCommunityAsync(communityId, ct);

        public UniTask<bool> SetMemberRoleAsync(string userId, string communityId, CommunityMemberRole newRole, CancellationToken ct) =>
            fakeDataProvider.SetMemberRoleAsync(userId, communityId, newRole, ct);
    }
}
