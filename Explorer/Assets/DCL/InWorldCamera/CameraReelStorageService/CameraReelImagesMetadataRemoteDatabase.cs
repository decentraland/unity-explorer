using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public class CameraReelImagesMetadataRemoteDatabase : ICameraReelImagesMetadataDatabase
    {
        private readonly IWebRequestController webRequestController;

        private readonly URLBuilder urlBuilder = new ();
        private readonly URLDomain imageDomain;
        private readonly URLDomain userDomain;

        public CameraReelImagesMetadataRemoteDatabase(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;

            imageDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.CameraReelImages));
            userDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.CameraReelUsers));
        }

        public async UniTask<CameraReelStorageResponse> GetStorageInfoAsync(string userAddress, CancellationToken ct)
        {
            URLAddress url = urlBuilder.AppendDomain(userDomain)
                                       .AppendSubDirectory(URLSubdirectory.FromString(userAddress))
                                       .Build();

            urlBuilder.Clear();

            CameraReelStorageResponse responseData = await webRequestController
                                                          .SignedFetchGetAsync(url, string.Empty, ct)
                                                          .CreateFromJson<CameraReelStorageResponse>(WRJsonParser.Unity);

            return responseData;
        }

        public async UniTask<CameraReelResponses> GetScreenshotsAsync(string userAddress, int limit, int offset, CancellationToken ct)
        {
            URLAddress url = urlBuilder.AppendDomain(userDomain)
                                       .AppendSubDirectory(URLSubdirectory.FromString(userAddress))
                                       .AppendSubDirectory(URLSubdirectory.FromString($"images?limit={limit}&offset={offset}"))
                                       .Build();

            urlBuilder.Clear();

            CameraReelResponses responseData = await webRequestController
                                                    .SignedFetchGetAsync(url, string.Empty, ct)
                                                    .CreateFromJson<CameraReelResponses>(WRJsonParser.Unity);

            return responseData;
        }

        public async UniTask<CameraReelStorageResponse> DeleteScreenshotAsync(string uuid, CancellationToken ct)
        {
            URLAddress url = urlBuilder.AppendDomain(imageDomain)
                                       .AppendSubDirectory(URLSubdirectory.FromString(uuid))
                                       .Build();

            urlBuilder.Clear();

            CameraReelStorageResponse responseData = await webRequestController
                                                          .SignedFetchDeleteAsync(url, string.Empty, ct)
                                                          .CreateFromJson<CameraReelStorageResponse>(WRJsonParser.Unity);

            return responseData;
        }

        public async UniTask UpdateScreenshotVisibilityAsync(string uuid, bool isPublic, CancellationToken ct)
        {
            URLAddress url = urlBuilder.AppendDomain(imageDomain)
                                       .AppendSubDirectory(URLSubdirectory.FromString(uuid))
                                       .AppendSubDirectory(URLSubdirectory.FromString("visibility"))
                                       .Build();

            urlBuilder.Clear();

            await webRequestController
                 .SignedFetchPatchAsync(url, GenericPatchArguments.CreateJson($"{{\"is_public\": {isPublic.ToString().ToLower()}}}"), string.Empty, ct)
                 .WithNoOpAsync();
        }

        public async UniTask<CameraReelUploadResponse> UploadScreenshotAsync(byte[] image, ScreenshotMetadata metadata, CancellationToken ct)
        {
            URLAddress url = urlBuilder.AppendDomain(imageDomain).Build();
            urlBuilder.Clear();

            var formData = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("image", image, $"{metadata.dateTime}.jpg", "image/jpeg"),
                new MultipartFormDataSection("metadata", JsonUtility.ToJson(metadata)),
            };

            CameraReelUploadResponse responseData = await webRequestController
                                                         .SignedFetchPostAsync(url, GenericPostArguments.CreateMultipartForm(formData), string.Empty, ct)
                                                         .CreateFromJson<CameraReelUploadResponse>(WRJsonParser.Unity);

            return responseData;
        }
    }
}
