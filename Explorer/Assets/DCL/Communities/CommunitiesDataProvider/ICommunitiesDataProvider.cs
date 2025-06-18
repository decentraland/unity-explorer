using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Communities
{
    public interface ICommunitiesDataProvider
    {
        public delegate void CommunityOperation(string communityId);
        event CommunityOperation CommunityUpdated;

        event Action CommunityCreated;
        event Action CommunityDeleted;

        UniTask<GetCommunityResponse> GetCommunityAsync(string communityId, CancellationToken ct);
        UniTask<GetUserCommunitiesResponse> GetUserCommunitiesAsync(string name, bool onlyMemberOf, int pageNumber, int elementsPerPage, CancellationToken ct);
        UniTask<GetUserLandsResponse> GetUserLandsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct);
        UniTask<GetUserWorldsResponse> GetUserWorldsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct);
        UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunityAsync(string communityId, string name, string description, byte[] thumbnail, List<string> lands, List<string> worlds, CancellationToken ct);
        UniTask<GetCommunityMembersResponse> GetCommunityMembersAsync(string communityId, int pageNumber, int elementsPerPage, CancellationToken ct);
        UniTask<GetCommunityMembersResponse> GetBannedCommunityMembersAsync(string communityId, int pageNumber, int elementsPerPage, CancellationToken ct);
        UniTask<GetUserCommunitiesCompactResponse> GetUserCommunitiesCompactAsync(CancellationToken ct);
        UniTask<List<string>> GetCommunityPlacesAsync(string communityId, CancellationToken ct);
        UniTask<GetCommunityMembersResponse> GetOnlineCommunityMembersAsync(string communityId, CancellationToken ct);
        UniTask<int> GetOnlineMemberCountAsync(string communityId, CancellationToken ct);

        UniTask<bool> KickUserFromCommunityAsync(string userId, string communityId, CancellationToken ct);
        UniTask<bool> BanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct);
        UniTask<bool> UnBanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct);
        UniTask<bool> LeaveCommunityAsync(string communityId, CancellationToken ct);
        UniTask<bool> JoinCommunityAsync(string communityId, CancellationToken ct);
        UniTask<bool> DeleteCommunityAsync(string communityId, CancellationToken ct);
        UniTask<bool> SetMemberRoleAsync(string userId, string communityId, CommunityMemberRole newRole, CancellationToken ct);
        UniTask<bool> RemovePlaceFromCommunityAsync(string communityId, string placeId, CancellationToken ct);
    }
}
