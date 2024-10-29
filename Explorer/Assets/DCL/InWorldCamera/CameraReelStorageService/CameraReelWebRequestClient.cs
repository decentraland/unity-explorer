using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;
using Utility.Times;

namespace DCL.InWorldCamera
{
    public class CameraReelWebRequestClient : ICameraReelNetworkClient
    {
        private readonly IWebRequestController webRequestController;
        private readonly IWeb3IdentityCache identityCache;

        private readonly IURLBuilder urlBuilder = new URLBuilder();
        private readonly URLDomain imageDomain;
        private readonly URLDomain userDomain;
        private readonly string imageBaseURL;
        private readonly string userBaseURL;

        public CameraReelWebRequestClient(IWeb3IdentityCache identityCache, IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.identityCache = identityCache;

            // imageBaseURL = $"https://camera-reel-service.decentraland.zone/api/images";
            // userBaseURL = $"https://camera-reel-service.decentraland.zone/api/users";
            imageDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.CameraReelImages));
            userDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.CameraReelUsers));
        }

        public void Test()
        {
            GetUserGalleryStorageInfoRequest(identityCache.Identity.Address, CancellationToken.None).Forget();
        }

        public async UniTask<CameraReelStorageResponse> GetUserGalleryStorageInfoRequest(string userAddress, CancellationToken ct)
        {
            var url = userDomain + "/"+ userAddress;
                // urlBuilder.AppendDomainWithReplacedPath(imageDomain, URLSubdirectory.FromString(userAddress)).Build();
            Debug.Log($"VVV-Sign: {url}");

            CameraReelStorageResponse responseData = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                                                                               .CreateFromJson<CameraReelStorageResponse>(WRJsonParser.Unity);

            Debug.Log(responseData.currentImages);
            Debug.Log(responseData.maxImages);
            //
            // CameraReelStorageResponse responseData =
                // await webRequestController.SignedFetchGetAsync(url, string.Empty, ct);
                                          // .CreateFromJson<CameraReelStorageResponse>(WRJsonParser.Unity);

            // UnityWebRequest result = await webRequestController.GetAsync($"{userBaseURL}/{userAddress}", isSigned: true, cancellationToken: ct);
            //
            // if (result.result != UnityWebRequest.Result.Success)
            //     throw new Exception($"Error fetching user gallery storage info :\n{result.error}");
            //
            // CameraReelStorageResponse responseData = Utils.SafeFromJson<CameraReelStorageResponse>(result.downloadHandler.text);
            //
            // if (responseData == null)
            //     throw new Exception($"Error parsing gallery storage info response:\n{result.downloadHandler.text}");

            // Debug.Log(responseData.currentImages);
            // Debug.Log(responseData.maxImages);
            //
            // return responseData;
            return new CameraReelStorageResponse();
        }

        public UniTask<CameraReelResponses> GetScreenshotGalleryRequest(string userAddress, int limit, int offset, CancellationToken ct) =>
            throw new NotImplementedException();

        public UniTask<CameraReelUploadResponse> UploadScreenshotRequest(byte[] image, ScreenshotMetadata metadata, CancellationToken ct) =>
            throw new NotImplementedException();

        public UniTask<CameraReelStorageResponse> DeleteScreenshotRequest(string uuid, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
