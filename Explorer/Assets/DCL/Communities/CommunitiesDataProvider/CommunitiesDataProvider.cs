using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.Communities
{
    public class CommunitiesDataProvider
    {
        public event Action<CreateOrUpdateCommunityResponse.CommunityData> CommunityCreated;
        public event Action<string> CommunityUpdated;
        public event Action<string> CommunityDeleted;
        public event Action<string, bool> CommunityJoined;
        public event Action<string, bool> CommunityLeft;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private Uri communitiesBaseUrl => urlsSource.Url(DecentralandUrl.Communities);

        public CommunitiesDataProvider(
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
            this.web3IdentityCache = web3IdentityCache;
        }

        public async UniTask<GetCommunityResponse> GetCommunityAsync(string communityId, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}");

            GetCommunityResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                                      .CreateFromJsonAsync<GetCommunityResponse>(WRJsonParser.Newtonsoft, ct);
            return response;
        }

        public async UniTask<GetUserCommunitiesResponse> GetUserCommunitiesAsync(string name, bool onlyMemberOf, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"?search={name}&onlyMemberOf={onlyMemberOf.ToString().ToLower()}&offset={(pageNumber * elementsPerPage) - elementsPerPage}&limit={elementsPerPage}");

            GetUserCommunitiesResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                                            .CreateFromJsonAsync<GetUserCommunitiesResponse>(WRJsonParser.Newtonsoft, ct);

            return response;
        }

        public async UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunityAsync(string communityId, string name, string description, byte[] thumbnail, List<string> lands, List<string> worlds, CancellationToken ct)
        {
            CreateOrUpdateCommunityResponse response;

            var formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("name", name),
                new MultipartFormDataSection("description", description),
            };

            StringBuilder placeIdsJsonString = new StringBuilder("[");
            for (var i = 0; i < lands.Count; i++)
            {
                placeIdsJsonString.Append($"\"{lands[i]}\"");
                if (i < lands.Count - 1)
                    placeIdsJsonString.Append(", ");
            }
            if (lands.Count > 0 && worlds.Count > 0)
                placeIdsJsonString.Append(", ");
            for (var i = 0; i < worlds.Count; i++)
            {
                placeIdsJsonString.Append($"\"{worlds[i]}\"");
                if (i < worlds.Count - 1)
                    placeIdsJsonString.Append(", ");
            }
            placeIdsJsonString.Append("]");
            formData.Add(new MultipartFormDataSection("placeIds", placeIdsJsonString.ToString()));

            if (thumbnail != null)
                formData.Add(new MultipartFormFileSection("thumbnail", thumbnail, "thumbnail.png", "image/png"));

            if (string.IsNullOrEmpty(communityId))
            {
                // Creating a new community
                response = await webRequestController.SignedFetchPostAsync(communitiesBaseUrl, GenericUploadArguments.CreateMultipartForm(formData), string.Empty, ReportCategory.COMMUNITIES)
                                                     .CreateFromJsonAsync<CreateOrUpdateCommunityResponse>(WRJsonParser.Newtonsoft, ct);

                CommunityCreated?.Invoke(response.data);
            }
            else
            {
                // Updating an existing community
                Uri communityEditionUrl = communitiesBaseUrl.Append($"/{communityId}");

                response = await webRequestController.SignedFetchPutAsync(communityEditionUrl, GenericUploadArguments.CreateMultipartForm(formData), string.Empty, ReportCategory.COMMUNITIES)
                                                     .CreateFromJsonAsync<CreateOrUpdateCommunityResponse>(WRJsonParser.Newtonsoft, ct);

                CommunityUpdated?.Invoke(communityId);
            }

            return response;
        }

        public async UniTask<GetCommunityMembersResponse> GetCommunityMembersAsync(string communityId, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}/members?offset={(pageNumber * elementsPerPage) - elementsPerPage}&limit={elementsPerPage}");

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                                             .CreateFromJsonAsync<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft, ct);
            return response;
        }

        public async UniTask<GetCommunityMembersResponse> GetBannedCommunityMembersAsync(string communityId, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}/bans?offset={(pageNumber * elementsPerPage) - elementsPerPage}&limit={elementsPerPage}");

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                                             .CreateFromJsonAsync<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft, ct);
            return response;
        }

        public async UniTask<GetCommunityMembersResponse> GetOnlineCommunityMembersAsync(string communityId, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}/members?offset=0&limit=1000&onlyOnline=true");

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                                             .CreateFromJsonAsync<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft, ct);
            return response;
        }

        public async UniTask<int> GetOnlineMemberCountAsync(string communityId, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}/members?offset=0&limit=0&onlyOnline=true");

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                                             .CreateFromJsonAsync<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft, ct);
            return response.data.total;
        }

        public async UniTask<List<string>> GetCommunityPlacesAsync(string communityId, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}/places");

            GetCommunityPlacesResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                                            .CreateFromJsonAsync<GetCommunityPlacesResponse>(WRJsonParser.Newtonsoft, ct);

            List<string> placesIds = new ();

            if (response is { data: { results: not null } })
            {
                foreach (GetCommunityPlacesResult placeResult in response.data.results)
                    placesIds.Add(placeResult.id);
            }

            return placesIds;
        }

        public UniTask<bool> KickUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            RemoveMemberFromCommunityAsync(userId, communityId, ct);

        public async UniTask<bool> BanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}/members/{userId}/bans");

            var result = await webRequestController.SignedFetchPostAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                   .SendAndForgetAsync(ct)
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        public async UniTask<bool> UnBanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}/members/{userId}/bans");

            var result = await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                   .SendAndForgetAsync(ct)
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        private async UniTask<bool> RemoveMemberFromCommunityAsync(string userId, string communityId, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}/members/{userId}");

            var result = await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                   .SendAndForgetAsync(ct)
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (web3IdentityCache.Identity?.Address == userId)
                CommunityLeft?.Invoke(communityId, result.Success);

            return result.Success;
        }

        public UniTask<bool> LeaveCommunityAsync(string communityId, CancellationToken ct) =>
            RemoveMemberFromCommunityAsync(web3IdentityCache.Identity?.Address, communityId, ct);

        public async UniTask<bool> JoinCommunityAsync(string communityId, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}/members");

            var result = await webRequestController.SignedFetchPostAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                   .SendAndForgetAsync(ct)
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            CommunityJoined?.Invoke(communityId, result.Success);

            return result.Success;
        }

        public async UniTask<bool> DeleteCommunityAsync(string communityId, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}");

            var result = await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                   .SendAndForgetAsync(ct)
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (result.Success)
                CommunityDeleted?.Invoke(communityId);

            return result.Success;
        }

        public async UniTask<bool> SetMemberRoleAsync(string userId, string communityId, CommunityMemberRole newRole, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}/members/{userId}");

            var result = await webRequestController.SignedFetchPatchAsync(url, GenericUploadArguments.CreateJson($"{{\"role\": \"{newRole.ToString()}\"}}"), string.Empty, ReportCategory.COMMUNITIES)
                                                   .SendAndForgetAsync(ct)
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        public async UniTask<bool> RemovePlaceFromCommunityAsync(string communityId, string placeId, CancellationToken ct)
        {
            Uri url = communitiesBaseUrl.Append($"/{communityId}/places/{placeId}");

            var result = await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ReportCategory.COMMUNITIES)
                                                   .SendAndForgetAsync(ct)
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        // TODO: Pending to implement these methods:
        //       public UniTask<GetUserLandsResponse> GetUserLandsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct)
        //       public UniTask<GetUserWorldsResponse> GetUserWorldsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct)
        //       public UniTask<GetUserCommunitiesCompactResponse> GetUserCommunitiesCompactAsync(CancellationToken ct)
    }
}
