using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Communities
{
    public interface ICommunitiesDataProvider
    {
        UniTask<GetUserCommunitiesResponse> GetUserCommunities(string userId, bool isOwner, bool isMember, int pageNumber, int elementsPerPage);
        UniTask<GetUserLandsResponse> GetUserLands(string userId, int pageNumber, int elementsPerPage);
        UniTask<GetUserWorldsResponse> GetUserWorlds(string userId, int pageNumber, int elementsPerPage);
        UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunity(string communityId, string name, string description, Span<byte> thumbnail, List<Vector2Int> lands, List<string> worlds);
        UniTask<GetCommunityMembersResponse> GetCommunityMembers(string communityId, bool areBanned, int pageNumber, int elementsPerPage);
        UniTask<GetCommunityPhotosResponse> GetCommunityPhotos(string communityId, int pageNumber, int elementsPerPage);
        UniTask<GetCommunityEventsResponse> GetCommunityEvents(string communityId, int pageNumber, int elementsPerPage);
        UniTask<GetCommunityPlacesResponse> GetCommunityPlaces(string communityId, int pageNumber, int elementsPerPage);
        UniTask<GetUserCommunitiesCompactResponse> GetUserCommunitiesCompact();
        UniTask<GetOnlineCommunityMembersResponse> GetOnlineCommunityMembers();

        UniTask<bool> KickUserFromCommunity(string userId, string communityId);
        UniTask<bool> BanUserFromCommunity(string userId, string communityId);
        UniTask<bool> LeaveCommunity(string communityId);
        UniTask<bool> JoinCommunity(string communityId);
        UniTask<bool> DeleteCommunity(string communityId);
    }
}
