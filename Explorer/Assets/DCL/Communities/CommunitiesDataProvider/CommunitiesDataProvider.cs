using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider.DTOs;
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

namespace DCL.Communities.CommunitiesDataProvider
{
    public class CommunitiesDataProvider
    {
        public event Action<CreateOrUpdateCommunityResponse.CommunityData> CommunityCreated;
        public event Action<string> CommunityUpdated;
        public event Action<string> CommunityDeleted;
        public event Action<string, bool> CommunityJoined;
        public event Action<string, bool> CommunityLeft;
        public event Action<string> CommunityUserRemoved;
        public event Action<string, string> CommunityUserBanned;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private string communitiesBaseUrl => urlsSource.Url(DecentralandUrl.Communities);

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
            var url = $"{communitiesBaseUrl}/{communityId}";

            GetCommunityResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                      .CreateFromJson<GetCommunityResponse>(WRJsonParser.Newtonsoft);
            return response;
        }

        public async UniTask<GetUserCommunitiesResponse> GetUserCommunitiesAsync(string name, bool onlyMemberOf, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}?search={name}&onlyMemberOf={onlyMemberOf.ToString().ToLower()}&offset={(pageNumber * elementsPerPage) - elementsPerPage}&limit={elementsPerPage}";

            GetUserCommunitiesResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                            .CreateFromJson<GetUserCommunitiesResponse>(WRJsonParser.Newtonsoft);

            return response;
        }

        public async UniTask<CreateOrUpdateCommunityResponse> CreateOrUpdateCommunityAsync(string communityId, string name, string description, byte[] thumbnail, List<string> lands, List<string> worlds, CommunityPrivacy privacy, CancellationToken ct)
        {
            CreateOrUpdateCommunityResponse response;

            var formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("name", name),
                new MultipartFormDataSection("description", description),
                new MultipartFormDataSection("privacy", privacy.ToString()),
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
                response = await webRequestController.SignedFetchPostAsync(communitiesBaseUrl, GenericPostArguments.CreateMultipartForm(formData), string.Empty, ct)
                                                     .CreateFromJson<CreateOrUpdateCommunityResponse>(WRJsonParser.Newtonsoft);

                CommunityCreated?.Invoke(response.data);
            }
            else
            {
                // Updating an existing community
                var communityEditionUrl = $"{communitiesBaseUrl}/{communityId}";
                response = await webRequestController.SignedFetchPutAsync(communityEditionUrl, GenericPutArguments.CreateMultipartForm(formData), string.Empty, ct)
                                                     .CreateFromJson<CreateOrUpdateCommunityResponse>(WRJsonParser.Newtonsoft);

                CommunityUpdated?.Invoke(communityId);
            }

            return response;
        }

        public async UniTask<GetCommunityMembersResponse> GetCommunityMembersAsync(string communityId, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}/members?offset={(pageNumber * elementsPerPage) - elementsPerPage}&limit={elementsPerPage}";

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                           .CreateFromJson<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft);
            return response;
        }

        public async UniTask<GetCommunityMembersResponse> GetBannedCommunityMembersAsync(string communityId, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}/bans?offset={(pageNumber * elementsPerPage) - elementsPerPage}&limit={elementsPerPage}";

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                             .CreateFromJson<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft);
            return response;
        }

        public async UniTask<GetCommunityMembersResponse> GetOnlineCommunityMembersAsync(string communityId, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}/members?offset=0&limit=1000&onlyOnline=true";

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                           .CreateFromJson<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft);
            return response;
        }

        public async UniTask<int> GetOnlineMemberCountAsync(string communityId, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}/members?offset=0&limit=0&onlyOnline=true";

            GetCommunityMembersResponse response = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                           .CreateFromJson<GetCommunityMembersResponse>(WRJsonParser.Newtonsoft);
            return response.data.total;
        }

        public async UniTask<List<string>> GetCommunityPlacesAsync(string communityId, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}/places";

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

        public UniTask<bool> KickUserFromCommunityAsync(string userId, string communityId, CancellationToken ct) =>
            RemoveMemberFromCommunityAsync(userId, communityId, ct);

        public async UniTask<bool> BanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}/members/{userId}/bans";

            var result = await webRequestController.SignedFetchPostAsync(url, string.Empty, ct)
                                                   .WithNoOpAsync()
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (result.Success)
                CommunityUserBanned?.Invoke(communityId, userId);

            return result.Success;
        }

        public async UniTask<bool> UnBanUserFromCommunityAsync(string userId, string communityId, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}/members/{userId}/bans";

            var result = await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ct)
                                                   .WithNoOpAsync()
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        private async UniTask<bool> RemoveMemberFromCommunityAsync(string userId, string communityId, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}/members/{userId}";

            var result = await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ct)
                                                   .WithNoOpAsync()
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (web3IdentityCache.Identity?.Address == userId)
                CommunityLeft?.Invoke(communityId, result.Success);
            else if (result.Success)
                CommunityUserRemoved?.Invoke(communityId);

            return result.Success;
        }

        public UniTask<bool> LeaveCommunityAsync(string communityId, CancellationToken ct) =>
            RemoveMemberFromCommunityAsync(web3IdentityCache.Identity?.Address, communityId, ct);

        public async UniTask<bool> JoinCommunityAsync(string communityId, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}/members";

            var result = await webRequestController.SignedFetchPostAsync(url, string.Empty, ct)
                                                   .WithNoOpAsync()
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            CommunityJoined?.Invoke(communityId, result.Success);

            return result.Success;
        }

        public async UniTask<bool> DeleteCommunityAsync(string communityId, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}";

            var result = await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ct)
                                      .WithNoOpAsync()
                                      .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (result.Success)
                CommunityDeleted?.Invoke(communityId);

            return result.Success;
        }

        public async UniTask<bool> SetMemberRoleAsync(string userId, string communityId, CommunityMemberRole newRole, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}/members/{userId}";

            var result = await webRequestController.SignedFetchPatchAsync(url, GenericPatchArguments.CreateJson($"{{\"role\": \"{newRole.ToString()}\"}}"), string.Empty, ct)
                                                   .WithNoOpAsync()
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        public async UniTask<bool> RemovePlaceFromCommunityAsync(string communityId, string placeId, CancellationToken ct)
        {
            var url = $"{communitiesBaseUrl}/{communityId}/places/{placeId}";

            var result = await webRequestController.SignedFetchDeleteAsync(url, string.Empty, ct)
                                                   .WithNoOpAsync()
                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            return result.Success;
        }

        // TODO: Pending to implement these methods:
        //       public UniTask<GetUserLandsResponse> GetUserLandsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct)
        //       public UniTask<GetUserWorldsResponse> GetUserWorldsAsync(string userId, int pageNumber, int elementsPerPage, CancellationToken ct)
        //       public UniTask<GetUserCommunitiesCompactResponse> GetUserCommunitiesCompactAsync(CancellationToken ct)
    }
}
