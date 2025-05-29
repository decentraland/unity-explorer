using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Communities
{
    public interface ICommunitiesDataProvider
    {
        UniTask<GetCommunityResponse> GetCommunityAsync(string communityId, CancellationToken ct);
        UniTask<GetUserCommunitiesResponse> GetUserCommunitiesAsync(string userId, string name, CommunityMemberRole[] memberRolesIncluded, int pageNumber, int elementsPerPage, CancellationToken ct);
        UniTask<GetUserLandsResponse> GetUserLandsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct);
        UniTask<GetUserWorldsResponse> GetUserWorldsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct);
        UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunityAsync(string communityId, string name, string description, byte[] thumbnail, List<Vector2Int> lands, List<string> worlds, CancellationToken ct);
        UniTask<GetCommunityMembersResponse> GetCommunityMembersAsync(string communityId, bool areBanned, int pageNumber, int elementsPerPage, CancellationToken ct);
        UniTask<GetUserCommunitiesCompactResponse> GetUserCommunitiesCompactAsync(CancellationToken ct);
        UniTask<GetOnlineCommunityMembersResponse> GetOnlineCommunityMembersAsync(CancellationToken ct);

        UniTask<bool> KickUserFromCommunityAsync(string userId, string communityId, CancellationToken ct);
        UniTask<bool> BanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct);
        UniTask<bool> UnBanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct);
        UniTask<bool> LeaveCommunityAsync(string communityId, CancellationToken ct);
        UniTask<bool> JoinCommunityAsync(string communityId, CancellationToken ct);
        UniTask<bool> DeleteCommunityAsync(string communityId, CancellationToken ct);
        UniTask<bool> SetMemberRoleAsync(string userId, string communityId, CommunityMemberRole newRole, CancellationToken ct);
    }
}
