using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Communities
{
    public class FakeCommunitiesDataProvider : ICommunitiesDataProvider
    {
        private readonly IAppArgs appArgs;

        public FakeCommunitiesDataProvider(IWebRequestController webRequestController,
            IWeb3IdentityCache web3IdentityCache,
            IDecentralandUrlsSource urlsSource,
            IAppArgs appArgs)
        {
            this.appArgs = appArgs;
        }

        public async UniTask<GetCommunityResponse> GetCommunityAsync(string communityId, CancellationToken ct)
        {
            CommunityMemberRole roleToReturn = CommunityMemberRole.member;

            if (appArgs.TryGetValue(AppArgsFlags.COMMUNITIES_CARD_SIMULATE_ROLE, out string? role) && role != null)
                if (Enum.TryParse(role, out CommunityMemberRole converted))
                    roleToReturn = converted;

            return new ()
            {
                community = new GetCommunityResponse.CommunityData
                {
                    id = communityId,
                    thumbnails = new string[] { "https://uchi.imgix.net/properties/anime2.png?crop=focalpoint&domain=uchi.imgix.net&fit=crop&fm=pjpg&fp-x=0.5&fp-y=0.5&h=558&ixlib=php-3.3.1&q=82&usm=20&w=992" },
                    name = "Fake Community",
                    description = "This is a fake community for testing purposes.",
                    ownerId = "0x31d4f4dd8615ec45bbb6330da69f60032aca219e",
                    privacy = CommunityPrivacy.@public,
                    role = roleToReturn,
                    places = new [] { "land1", "land2" },
                    membersCount = Random.Range(1, 1_000_000_000),
                }
            };
        }

        public async UniTask<GetUserCommunitiesResponse> GetUserCommunitiesAsync(string userId, string name, CommunityMemberRole[] memberRolesIncluded, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetUserLandsResponse> GetUserLandsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetUserWorldsResponse> GetUserWorldsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunityAsync(string communityId, string name, string description, byte[] thumbnail, List<Vector2Int> lands,
            List<string> worlds, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetCommunityMembersResponse> GetCommunityMembersAsync(string communityId, bool areBanned, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            const int TOTAL_MEMBERS = 15, BANNED_MEMBERS = 5;

            int membersToReturn = areBanned ? BANNED_MEMBERS : TOTAL_MEMBERS;

            GetCommunityMembersResponse.MemberData[] members = new GetCommunityMembersResponse.MemberData[membersToReturn];

            for (int i = 0; i < membersToReturn; i++)
                members[i] = GetCommunityMembersResponse.MemberData.RandomMember();

            GetCommunityMembersResponse result = new GetCommunityMembersResponse
                {
                    totalAmount = membersToReturn,
                    members = members,
                };

            return result;
        }

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
