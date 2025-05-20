using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Communities
{
    public class FakeCommunitiesDataProvider : ICommunitiesDataProvider
    {
        public FakeCommunitiesDataProvider(IWebRequestController webRequestController, IWeb3IdentityCache web3IdentityCache, IDecentralandUrlsSource urlsSource)
        {

        }

        public UniTask<GetUserCommunitiesResponse> GetUserCommunities(string userId, bool isOwner, bool isMember, int pageNumber, int elementsPerPage) =>
            throw new NotImplementedException();

        public UniTask<GetUserLandsResponse> GetUserLands(string userId, int pageNumber, int elementsPerPage) =>
            throw new NotImplementedException();

        public UniTask<GetUserWorldsResponse> GetUserWorlds(string userId, int pageNumber, int elementsPerPage) =>
            throw new NotImplementedException();

        public UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunity(string communityId, string name, string description, Span<byte> thumbnail, List<Vector2Int> lands,
            List<string> worlds) =>
            throw new NotImplementedException();

        public UniTask<GetCommunityMembersResponse> GetCommunityMembers(string communityId, bool areBanned, int pageNumber, int elementsPerPage) =>
            throw new NotImplementedException();

        public UniTask<GetCommunityPhotosResponse> GetCommunityPhotos(string communityId, int pageNumber, int elementsPerPage) =>
            throw new NotImplementedException();

        public UniTask<GetCommunityEventsResponse> GetCommunityEvents(string communityId, int pageNumber, int elementsPerPage) =>
            throw new NotImplementedException();

        public UniTask<GetCommunityPlacesResponse> GetCommunityPlaces(string communityId, int pageNumber, int elementsPerPage) =>
            throw new NotImplementedException();

        public UniTask<GetUserCommunitiesCompactResponse> GetUserCommunitiesCompact() =>
            throw new NotImplementedException();

        public UniTask<GetOnlineCommunityMembersResponse> GetOnlineCommunityMembers() =>
            throw new NotImplementedException();

        public UniTask<bool> KickUserFromCommunity(string userId, string communityId) =>
            throw new NotImplementedException();

        public UniTask<bool> BanUserFromCommunity(string userId, string communityId) =>
            throw new NotImplementedException();

        public UniTask<bool> LeaveCommunity(string communityId) =>
            throw new NotImplementedException();

        public UniTask<bool> JoinCommunity(string communityId) =>
            throw new NotImplementedException();

        public UniTask<bool> DeleteCommunity(string communityId) =>
            throw new NotImplementedException();
    }
}
