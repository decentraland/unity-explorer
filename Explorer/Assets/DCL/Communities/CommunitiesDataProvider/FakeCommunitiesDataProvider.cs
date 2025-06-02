using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Communities
{
    public class FakeCommunitiesDataProvider : ICommunitiesDataProvider
    {
        private readonly List<GetUserCommunitiesResponse.CommunityData> currentCommunities;

        public FakeCommunitiesDataProvider(IWebRequestController webRequestController,
            IWeb3IdentityCache web3IdentityCache,
            IDecentralandUrlsSource urlsSource)
        {
            currentCommunities = GetFakeCommunitiesForBrowserTesting(communitiesAsOwner: 1, communitiesAsModerator: 1, communitiesAsMember: 1);
        }

        public async UniTask<GetCommunityResponse> GetCommunityAsync(string communityId, CancellationToken ct)
        {
            GetUserCommunitiesResponse.CommunityData communityData = currentCommunities.Find(community => community.id == communityId);

            return new GetCommunityResponse()
            {
                community = new GetCommunityResponse.CommunityData()
                {
                    id = communityData.id,
                    thumbnails = communityData.thumbnails,
                    name = communityData.name,
                    description = communityData.description,
                    ownerId = communityData.ownerId,
                    memberCount = communityData.memberCount,
                    privacy = communityData.privacy,
                    role = communityData.role,
                    places = new [] { "land1", "land2" },
                    membersCount = communityData.memberCount,
                }
            };
        }

        public async UniTask<GetUserCommunitiesResponse> GetUserCommunitiesAsync(string userId, string name, CommunityMemberRole[] memberRolesIncluded, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            List<GetUserCommunitiesResponse.CommunityData> filteredCommunities = currentCommunities
                                                                                .Where(x => (
                                                                                                (memberRolesIncluded.ToList().Contains(CommunityMemberRole.owner) && x.role == CommunityMemberRole.owner) ||
                                                                                                (memberRolesIncluded.ToList().Contains(CommunityMemberRole.moderator) && x.role == CommunityMemberRole.moderator) ||
                                                                                                (memberRolesIncluded.ToList().Contains(CommunityMemberRole.member) && x.role == CommunityMemberRole.member) ||
                                                                                                (memberRolesIncluded.ToList().Contains(CommunityMemberRole.none) && x.role == CommunityMemberRole.none)) &&
                                                                                            (x.name.ToLower().Contains(name.ToLower()) || x.description.ToLower().Contains(name.ToLower())))
                                                                                .ToList();

            List<GetUserCommunitiesResponse.CommunityData> paginatedCommunities = new();
            for (var i = 0; i < filteredCommunities.Count; i++)
            {
                if (i >= (pageNumber - 1) * elementsPerPage && i < pageNumber * elementsPerPage)
                    paginatedCommunities.Add(filteredCommunities[i]);
            }

            GetUserCommunitiesResponse result = new GetUserCommunitiesResponse
            {
                communities = paginatedCommunities.ToArray(),
                totalAmount = filteredCommunities.Count,
            };

            await UniTask.Delay(UnityEngine.Random.Range(1000, 2000), cancellationToken: ct);

            return result;
        }

        public async UniTask<GetUserLandsResponse> GetUserLandsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetUserWorldsResponse> GetUserWorldsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunityAsync(string communityId, string name, string description, byte[] thumbnail, List<Vector2Int> lands,
            List<string> worlds, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetCommunityMembersResponse> GetCommunityMembersAsync(string communityId, bool areBanned, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            GetUserCommunitiesResponse.CommunityData communityData = currentCommunities.Find(community => community.id == communityId);

            const int BANNED_MEMBERS = 5;

            int totalMembers = areBanned ? BANNED_MEMBERS : communityData.memberCount;

            List<GetCommunityMembersResponse.MemberData> paginatedData = new ();

            for (var i = 0; i < totalMembers; i++)
            {
                if (i >= (pageNumber - 1) * elementsPerPage && i < pageNumber * elementsPerPage)
                {
                    GetCommunityMembersResponse.MemberData member = GetRandomMember();

                    if (areBanned)
                        member.role = CommunityMemberRole.none;

                    paginatedData.Add(member);
                }
            }

            GetCommunityMembersResponse result = new GetCommunityMembersResponse
            {
                totalAmount = totalMembers,
                members = paginatedData.ToArray(),
            };

            return result;
        }

        public async UniTask<GetUserCommunitiesCompactResponse> GetUserCommunitiesCompactAsync(CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetOnlineCommunityMembersResponse> GetOnlineCommunityMembersAsync(CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<bool> KickUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            true;

        public async UniTask<bool> BanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            true;

        public async UniTask<bool> UnBanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            true;

        public async UniTask<bool> LeaveCommunityAsync(string communityId, CancellationToken ct)
        {
            await UniTask.Delay(UnityEngine.Random.Range(1000, 2000), cancellationToken: ct);

            foreach (GetUserCommunitiesResponse.CommunityData community in currentCommunities)
            {
                if (community.id == communityId)
                {
                    community.role = CommunityMemberRole.none;
                    break;
                }
            }

            return true;
        }

        public async UniTask<bool> JoinCommunityAsync(string communityId, CancellationToken ct)
        {
            await UniTask.Delay(UnityEngine.Random.Range(1000, 2000), cancellationToken: ct);

            foreach (GetUserCommunitiesResponse.CommunityData community in currentCommunities)
            {
                if (community.id == communityId)
                {
                    community.role = CommunityMemberRole.member;
                    break;
                }
            }

            return true;
        }

        public async UniTask<bool> DeleteCommunityAsync(string communityId, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<bool> SetMemberRoleAsync(string userId, string communityId,  CommunityMemberRole newRole, CancellationToken ct) =>
            true;

        private List<GetUserCommunitiesResponse.CommunityData> GetFakeCommunitiesForBrowserTesting(int communitiesAsOwner, int communitiesAsModerator, int communitiesAsMember)
        {
            List<GetUserCommunitiesResponse.CommunityData> communities = new List<GetUserCommunitiesResponse.CommunityData>();

            for (var i = 0; i < 100; i++)
            {
                List<GetUserCommunitiesResponse.FriendInCommunity> mutualFriends = new ();
                int amountMutualFriends = UnityEngine.Random.Range(0, 4);
                for (var j = 0; j < amountMutualFriends; j++)
                {
                    mutualFriends.Add(new GetUserCommunitiesResponse.FriendInCommunity
                    {
                        id = $"test{i + 1}",
                        name = $"testUser{i + 1}",
                        profilePictureUrl = "https://picsum.photos/20/20",
                    });
                }

                communities.Add(new GetUserCommunitiesResponse.CommunityData
                {
                    id = (i + 1).ToString(),
                    thumbnails = new[] { "https://picsum.photos/280/280" },
                    name = $"Community {i + 1}",
                    description = $"Test description for Community {i + 1}. This is only a fake text to test this awesome feature!! This is the card that represent a community in Decentraland.",
                    ownerId = string.Empty,
                    privacy = i is 3 or 5 ? CommunityPrivacy.@private : CommunityPrivacy.@public,
                    role = i < communitiesAsOwner ? CommunityMemberRole.owner :
                        i < communitiesAsOwner + communitiesAsModerator ? CommunityMemberRole.moderator :
                        i < communitiesAsOwner + communitiesAsModerator + communitiesAsMember ? CommunityMemberRole.member : CommunityMemberRole.none,
                    memberCount = UnityEngine.Random.Range(1, 101),
                    isLive = UnityEngine.Random.Range(0, 5) == 0,
                    friends = mutualFriends.ToArray(),
                });
            }

            return communities;
        }

        private const string HEX_CHARS = "0123456789abcdef";
        private static readonly string[] ADJECTIVES =
        {
            "cool", "fast", "silent", "happy", "dark", "bright",
            "blue", "frozen", "angry", "brave", "smart", "wild"
        };

        private static readonly string[] NOUNS =
        {
            "fox", "wolf", "rider", "ghost", "cat", "hawk", "stone",
            "blade", "shadow", "storm", "dragon", "raven"
        };

        private static readonly CommunityMemberRole[] ROLES = EnumUtils.Values<CommunityMemberRole>().Where(role => role != CommunityMemberRole.none).ToArray();
        private static readonly FriendshipStatus[] FRIENDSHIP_STATUSES = EnumUtils.Values<FriendshipStatus>();

        public static GetCommunityMembersResponse.MemberData GetRandomMember()
        {
            var sb = new StringBuilder("0x");

            for (int i = 0; i < 40; i++)
                sb.Append(HEX_CHARS[UnityEngine.Random.Range(0, HEX_CHARS.Length)]);

            return new GetCommunityMembersResponse.MemberData(sb.ToString(),
                "",
                $"{ADJECTIVES[UnityEngine.Random.Range(0, ADJECTIVES.Length)]}{NOUNS[UnityEngine.Random.Range(0, NOUNS.Length)]}",
                UnityEngine.Random.Range(0, 100) > 50,
                ROLES[UnityEngine.Random.Range(0, ROLES.Length)],
                UnityEngine.Random.Range(0, 10),
                FRIENDSHIP_STATUSES[UnityEngine.Random.Range(0, FRIENDSHIP_STATUSES.Length)]);
        }
    }
}
