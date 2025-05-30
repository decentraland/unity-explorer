using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
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

        public UniTask<GetCommunityResponse> GetCommunityAsync(string communityId, CancellationToken ct) =>
            fakeDataProvider.GetCommunityAsync(communityId, ct);

        public UniTask<GetUserCommunitiesResponse> GetUserCommunitiesAsync(string userId, string name, CommunityMemberRole[] memberRolesIncluded, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            fakeDataProvider.GetUserCommunitiesAsync(userId, name, memberRolesIncluded, pageNumber, elementsPerPage, ct);

        public UniTask<GetUserLandsResponse> GetUserLandsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            fakeDataProvider.GetUserLandsAsync(userId, pageNumber, elementsPerPage, ct);

        public UniTask<GetUserWorldsResponse> GetUserWorldsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            fakeDataProvider.GetUserWorldsAsync(userId, pageNumber, elementsPerPage, ct);

        public UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunityAsync(string communityId, string name, string description, byte[] thumbnail, List<Vector2Int> lands, List<string> worlds, CancellationToken ct) =>
            fakeDataProvider.CreateOrUpdateCommunityAsync(communityId, name, description, thumbnail, lands, worlds, ct);

        public UniTask<GetCommunityMembersResponse> GetCommunityMembersAsync(string communityId, bool areBanned, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            fakeDataProvider.GetCommunityMembersAsync(communityId, areBanned, pageNumber, elementsPerPage, ct);

        public UniTask<GetUserCommunitiesCompactResponse> GetUserCommunitiesCompactAsync(CancellationToken ct) =>
            fakeDataProvider.GetUserCommunitiesCompactAsync(ct);

        public UniTask<GetOnlineCommunityMembersResponse> GetOnlineCommunityMembersAsync(CancellationToken ct) =>
            fakeDataProvider.GetOnlineCommunityMembersAsync(ct);

        public UniTask<bool> KickUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            fakeDataProvider.KickUserFromCommunityAsync(userId, communityId, ct);

        public UniTask<bool> BanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            fakeDataProvider.BanUserFromCommunityAsync(userId, communityId, ct);

        public UniTask<bool> LeaveCommunityAsync(string communityId, CancellationToken ct) =>
            fakeDataProvider.LeaveCommunityAsync(communityId, ct);

        public UniTask<bool> JoinCommunityAsync(string communityId, CancellationToken ct) =>
            fakeDataProvider.JoinCommunityAsync(communityId, ct);

        public UniTask<bool> DeleteCommunityAsync(string communityId, CancellationToken ct) =>
            fakeDataProvider.DeleteCommunityAsync(communityId, ct);

        public UniTask<bool> SetMemberRoleAsync(string userId, string communityId, CancellationToken ct) =>
            fakeDataProvider.SetMemberRoleAsync(userId, communityId, ct);
    }
}
