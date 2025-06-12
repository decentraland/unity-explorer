using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
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
        private readonly List<GetUserCommunitiesData.CommunityData> currentCommunities;

        public FakeCommunitiesDataProvider(IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource)
        {
            currentCommunities = GetFakeCommunitiesForBrowserTesting(communitiesAsOwner: 1, communitiesAsModerator: 1, communitiesAsMember: 1);
        }

        public async UniTask<GetCommunityResponse> GetCommunityAsync(string communityId, CancellationToken ct)
        {
            await UniTask.Delay(UnityEngine.Random.Range(1000, 2000), cancellationToken: ct);

            GetUserCommunitiesData.CommunityData communityData = currentCommunities.Find(community => community.id == communityId);

            return new GetCommunityResponse()
            {
                data = new GetCommunityResponse.CommunityData()
                {
                    id = communityData.id,
                    thumbnails = communityData.thumbnails,
                    name = communityData.name,
                    description = communityData.description,
                    ownerAddress = "test",
                    privacy = communityData.privacy,
                    role = communityData.role,
                    places = new [] { "land1", "land2" },
                    membersCount = communityData.membersCount,
                }
            };
        }

        public async UniTask<GetUserCommunitiesResponse> GetUserCommunitiesAsync(string name, bool onlyMemberOf, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            List<GetUserCommunitiesData.CommunityData> filteredCommunities = currentCommunities
                                                                            .Where(x => (
                                                                                            (onlyMemberOf && x.role == CommunityMemberRole.owner) ||
                                                                                            (onlyMemberOf && x.role == CommunityMemberRole.moderator) ||
                                                                                            (onlyMemberOf && x.role == CommunityMemberRole.member) ||
                                                                                            (!onlyMemberOf && x.role == CommunityMemberRole.none)) &&
                                                                                        (x.name.ToLower().Contains(name.ToLower()) || x.description.ToLower().Contains(name.ToLower())))
                                                                            .ToList();

            List<GetUserCommunitiesData.CommunityData> paginatedCommunities = new();
            for (var i = 0; i < filteredCommunities.Count; i++)
            {
                if (i >= (pageNumber - 1) * elementsPerPage && i < pageNumber * elementsPerPage)
                    paginatedCommunities.Add(filteredCommunities[i]);
            }

            GetUserCommunitiesResponse result = new GetUserCommunitiesResponse
            {
                data = new GetUserCommunitiesData
                {
                    results = paginatedCommunities.ToArray(),
                    total = filteredCommunities.Count,
                },
            };

            await UniTask.Delay(UnityEngine.Random.Range(1000, 2000), cancellationToken: ct);

            return result;
        }

        public async UniTask<GetUserLandsResponse> GetUserLandsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetUserWorldsResponse> GetUserWorldsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunityAsync(string communityId, string name, string description, byte[] thumbnail, List<string> lands,
            List<string> worlds, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetCommunityMembersResponse> GetCommunityMembersAsync(string communityId, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            GetUserCommunitiesData.CommunityData communityData = currentCommunities.Find(community => community.id == communityId);

            int totalMembers = communityData.membersCount;

            List<GetCommunityMembersResponse.MemberData> paginatedData = new ();

            for (var i = 0; i < totalMembers; i++)
            {
                if (i >= (pageNumber - 1) * elementsPerPage && i < pageNumber * elementsPerPage)
                {
                    GetCommunityMembersResponse.MemberData member = GetRandomMember();

                    paginatedData.Add(member);
                }
            }

            GetCommunityMembersResponse result = new GetCommunityMembersResponse
            {
                data = new ()
                {
                    total = totalMembers,
                    results = paginatedData.ToArray(),
                }
            };

            return result;
        }

        public async UniTask<GetCommunityMembersResponse> GetBannedCommunityMembersAsync(string communityId, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            const int BANNED_MEMBERS = 5;

            List<GetCommunityMembersResponse.MemberData> paginatedData = new ();

            for (var i = 0; i < BANNED_MEMBERS; i++)
            {
                if (i >= (pageNumber - 1) * elementsPerPage && i < pageNumber * elementsPerPage)
                {
                    GetCommunityMembersResponse.MemberData member = GetRandomMember();

                    member.role = CommunityMemberRole.none;

                    paginatedData.Add(member);
                }
            }

            GetCommunityMembersResponse result = new GetCommunityMembersResponse
            {
                data = new ()
                {
                    total = BANNED_MEMBERS,
                    results = paginatedData.ToArray(),
                }
            };

            return result;
        }

        public async UniTask<GetUserCommunitiesCompactResponse> GetUserCommunitiesCompactAsync(CancellationToken ct)
        {
            await UniTask.Delay(500, DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
            return new GetUserCommunitiesCompactResponse()
            {
                communities = new []
                {
                    new GetUserCommunitiesCompactResponse.CommunityData()
                    {
                        name = "Community 5",
                        id= "c5e5a5b6-5555-4a5b-9555-555555555555",
                        role = CommunityMemberRole.moderator,
                        memberCount = 10,
                        ownerId = "0xf118c24c103a4ba9e13d9db8f2747efe87be507f",
                        smallThumbnail = "https://profile-images.decentraland.org/entities/bafkreierrpokjlha5fqj43n3yxe2jkgrrbgekre6ymeh7bi6enkgxcwa3e/face.png"
                    }
                }
            };
        }

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

            foreach (GetUserCommunitiesData.CommunityData community in currentCommunities)
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

            foreach (GetUserCommunitiesData.CommunityData community in currentCommunities)
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

        private List<GetUserCommunitiesData.CommunityData> GetFakeCommunitiesForBrowserTesting(int communitiesAsOwner, int communitiesAsModerator, int communitiesAsMember)
        {
            List<GetUserCommunitiesData.CommunityData> communities = new List<GetUserCommunitiesData.CommunityData>();

            for (var i = 0; i < 100; i++)
            {
                List<GetUserCommunitiesData.FriendInCommunity> mutualFriends = new ();
                int amountMutualFriends = UnityEngine.Random.Range(0, 4);
                for (var j = 0; j < amountMutualFriends; j++)
                {
                    mutualFriends.Add(new GetUserCommunitiesData.FriendInCommunity
                    {
                        address = $"test{i + 1}",
                        name = $"testUser{i + 1}",
                        profilePictureUrl = "https://picsum.photos/20/20",
                    });
                }

                communities.Add(new GetUserCommunitiesData.CommunityData
                {
                    id = (i + 1).ToString(),
                    thumbnails = new CommunityThumbnails { raw = "https://picsum.photos/280/280" },
                    name = $"Community {i + 1}",
                    description = $"Test description for Community {i + 1}. This is only a fake text to test this awesome feature!! This is the card that represent a community in Decentraland.",
                    ownerAddress = string.Empty,
                    privacy = i is 3 or 5 ? CommunityPrivacy.@private : CommunityPrivacy.@public,
                    role = i < communitiesAsOwner ? CommunityMemberRole.owner :
                        i < communitiesAsOwner + communitiesAsModerator ? CommunityMemberRole.moderator :
                        i < communitiesAsOwner + communitiesAsModerator + communitiesAsMember ? CommunityMemberRole.member : CommunityMemberRole.none,
                    membersCount = UnityEngine.Random.Range(1, 101),
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

        private static GetCommunityMembersResponse.MemberData GetRandomMember()
        {
            var sb = new StringBuilder("0x");

            for (int i = 0; i < 40; i++)
                sb.Append(HEX_CHARS[UnityEngine.Random.Range(0, HEX_CHARS.Length)]);

            return new GetCommunityMembersResponse.MemberData()
            {
                memberAddress = sb.ToString(),
                name = $"{ADJECTIVES[UnityEngine.Random.Range(0, ADJECTIVES.Length)]}{NOUNS[UnityEngine.Random.Range(0, NOUNS.Length)]}",
                hasClaimedName = UnityEngine.Random.Range(0, 100) > 50,
                role = ROLES[UnityEngine.Random.Range(0, ROLES.Length)],
                friendshipStatus = FRIENDSHIP_STATUSES[UnityEngine.Random.Range(0, FRIENDSHIP_STATUSES.Length)]
            };
        }
    }
}
