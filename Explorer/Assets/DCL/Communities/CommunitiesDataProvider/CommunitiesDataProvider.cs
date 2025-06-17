using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.Communities
{
    public class CommunitiesDataProvider : ICommunitiesDataProvider
    {
        private readonly ICommunitiesDataProvider fakeDataProvider;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private string communitiesBaseUrl => urlsSource.Url(DecentralandUrl.Communities);

        public CommunitiesDataProvider(
            ICommunitiesDataProvider fakeDataProvider,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.fakeDataProvider = fakeDataProvider;
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
            this.web3IdentityCache = web3IdentityCache;
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

            GetUserCommunitiesResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                            .CreateFromJson<GetUserCommunitiesResponse>(WRJsonParser.Newtonsoft);

            return response;
        }

        public UniTask<GetUserLandsResponse> GetUserLandsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            fakeDataProvider.GetUserLandsAsync(userId, pageNumber, elementsPerPage, ct);

        public UniTask<GetUserWorldsResponse> GetUserWorldsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            fakeDataProvider.GetUserWorldsAsync(userId, pageNumber, elementsPerPage, ct);

        public async UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunityAsync(string communityId, string name, string description, byte[] thumbnail, List<string> lands, List<string> worlds, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/communities";

            CreateOrUpdateCommunityResponse response;
            if (string.IsNullOrEmpty(communityId))
            {
                // Creating a new community
                var formData = new List<IMultipartFormSection>
                {
                    new MultipartFormDataSection("name", name),
                    new MultipartFormDataSection("description", description),
                };

                if (thumbnail != null)
                    formData.Add(new MultipartFormFileSection("thumbnail", thumbnail, "thumbnail.png", "image/png"));

                response = await webRequestController.SignedFetchPostAsync(url, GenericPostArguments.CreateMultipartForm(formData), string.Empty, ct)
                                                     .CreateFromJson<CreateOrUpdateCommunityResponse>(WRJsonParser.Newtonsoft);
            }
            else
            {
                // Updating an existing community
                throw new NotImplementedException("Updating communities is not implemented yet.");
            }

            return response;
        }

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

        public async UniTask<GetCommunityMembersResponse> GetOnlineCommunityMembersAsync(string communityId, CancellationToken ct)
        {
            string url = $"{communitiesBaseUrl}/communities/{communityId}/members?offset=0&limit=1000&onlyOnline=true";

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                           .CreateFromJson<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft);
            return response;
        }

        public async UniTask<int> GetOnlineMemberCountAsync(string communityId, CancellationToken ct)
        {
            string url = $"{communitiesBaseUrl}/communities/{communityId}/members?offset=0&limit=0&onlyOnline=true";

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                           .CreateFromJson<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft);
            return response.data.total;
        }

        public async UniTask<List<string>> GetCommunityPlacesAsync(string communityId, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/communities/{communityId}/places";

            GetCommunityPlacesResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                            .CreateFromJson<GetCommunityPlacesResponse>(WRJsonParser.Newtonsoft);

            List<string> placesIds = new ();

            if (response is { data: { results: not null } })
            {
                foreach (GetCommunityPlacesResult placeResult in response.data.results)
                    placesIds.Add(placeResult.id);
            }

            return placesIds;
        }

        public UniTask<CommunityEventsResponse> GetCommunityEventsAsync(string communityId, int pageNumber, int elementsPerPage, CancellationToken ct) =>
            fakeDataProvider.GetCommunityEventsAsync(communityId, pageNumber, elementsPerPage, ct);

        public UniTask<bool> KickUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            RemoveMemberFromCommunityAsync(userId, communityId, ct);

        public async UniTask<bool> BanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct)
        {
            string url = $"{communitiesBaseUrl}/communities/{communityId}/members/{userId}/bans";

            var result = await webRequestController.SignedFetchPostAsync(url, string.Empty, ct)
                                                   .WithNoOpAsync()
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        public async UniTask<bool> UnBanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct)
        {
            string url = $"{communitiesBaseUrl}/communities/{communityId}/members/{userId}/bans";

            var result = await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ct)
                                                   .WithNoOpAsync()
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        private async UniTask<bool> RemoveMemberFromCommunityAsync(string userId, string communityId, CancellationToken ct)
        {
            string url = $"{communitiesBaseUrl}/communities/{communityId}/members/{userId}";

            var result = await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ct)
                                                   .WithNoOpAsync()
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        public UniTask<bool> LeaveCommunityAsync(string communityId, CancellationToken ct) =>
            RemoveMemberFromCommunityAsync(web3IdentityCache.Identity?.Address, communityId, ct);

        public async UniTask<bool> JoinCommunityAsync(string communityId, CancellationToken ct)
        {
            string url = $"{communitiesBaseUrl}/communities/{communityId}/members";

            var result = await webRequestController.SignedFetchPostAsync(url, string.Empty, ct)
                                                   .WithNoOpAsync()
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        public async UniTask<bool> DeleteCommunityAsync(string communityId, CancellationToken ct)
        {
            string url = $"{communitiesBaseUrl}/communities/{communityId}";

            var result = await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ct)
                                      .WithNoOpAsync()
                                      .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        public async UniTask<bool> SetMemberRoleAsync(string userId, string communityId, CommunityMemberRole newRole, CancellationToken ct)
        {
            string url = $"{communitiesBaseUrl}/communities/{communityId}/members/{userId}";

            var result = await webRequestController.SignedFetchPatchAsync(url, GenericPatchArguments.CreateJson($"{{\"role\": \"{newRole.ToString()}\"}}"), string.Empty, ct)
                                                   .WithNoOpAsync()
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }
    }
}
