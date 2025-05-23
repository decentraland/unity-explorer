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
    public class FakeCommunitiesDataProvider : ICommunitiesDataProvider
    {
        public FakeCommunitiesDataProvider(IWebRequestController webRequestController, IWeb3IdentityCache web3IdentityCache, IDecentralandUrlsSource urlsSource)
        {

        }

        public async UniTask<GetCommunityResponse> GetCommunityAsync(string communityId, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetUserCommunitiesResponse> GetUserCommunitiesAsync(string userId, string name, bool isOwner, bool isMember, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            throw new NotImplementedException();

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
    }
}
