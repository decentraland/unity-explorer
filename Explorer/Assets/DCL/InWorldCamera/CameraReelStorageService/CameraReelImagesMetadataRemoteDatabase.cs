using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
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
        private readonly URLDomain placesDomain;

        public CameraReelImagesMetadataRemoteDatabase(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;

            imageDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.CameraReelImages));
            userDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.CameraReelUsers));
            placesDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.CameraReelPlaces));
        }

        public async UniTask<CameraReelStorageResponse> GetStorageInfoAsync(string userAddress, CancellationToken ct)
        {
            var url = urlBuilder.AppendDomain(userDomain)
                                .AppendSubDirectory(URLSubdirectory.FromString(userAddress))
                                .Build();

            urlBuilder.Clear();

            CameraReelStorageResponse responseData = await webRequestController
                                                          .SignedFetchGetAsync(url, string.Empty, ReportCategory.CAMERA_REEL)
                                                          .CreateFromJsonAsync<CameraReelStorageResponse>(WRJsonParser.Unity, ct);

            return responseData;
        }

        public async UniTask<CameraReelStorageResponse> UnsignedGetStorageInfoAsync(string userAddress, CancellationToken ct)
        {
            var url = urlBuilder.AppendDomain(userDomain)
                                .AppendSubDirectory(URLSubdirectory.FromString(userAddress))
                                .Build();

            urlBuilder.Clear();

            CameraReelStorageResponse responseData = await webRequestController
                                                          .GetAsync(url, ReportCategory.CAMERA_REEL)
                                                          .CreateFromJsonAsync<CameraReelStorageResponse>(WRJsonParser.Unity, ct);

            return responseData;
        }

        public async UniTask<CameraReelResponse> GetScreenshotsMetadataAsync(string uuid, CancellationToken ct)
        {
            var url = urlBuilder.AppendDomain(imageDomain)
                                .AppendSubDirectory(URLSubdirectory.FromString(uuid))
                                .AppendSubDirectory(URLSubdirectory.FromString("metadata"))
                                .Build();

            urlBuilder.Clear();

            CameraReelResponse responseData = await webRequestController
                                                   .SignedFetchGetAsync(url, string.Empty, ReportCategory.CAMERA_REEL)
                                                   .CreateFromJsonAsync<CameraReelResponse>(WRJsonParser.Unity, ct);

            return responseData;
        }

        public async UniTask<CameraReelResponses> GetScreenshotsAsync(string userAddress, int limit, int offset, CancellationToken ct)
        {
            var url = urlBuilder.AppendDomain(userDomain)
                                .AppendSubDirectory(URLSubdirectory.FromString(userAddress))
                                .AppendSubDirectory(URLSubdirectory.FromString($"images?limit={limit}&offset={offset}"))
                                .Build();

            urlBuilder.Clear();

            CameraReelResponses responseData = await webRequestController
                                                    .SignedFetchGetAsync(url, string.Empty, ReportCategory.CAMERA_REEL)
                                                    .CreateFromJsonAsync<CameraReelResponses>(WRJsonParser.Unity, ct);

            return responseData;
        }

        public async UniTask<CameraReelResponsesCompact> GetCompactScreenshotsAsync(string userAddress, int limit, int offset, CancellationToken ct)
        {
            var url = urlBuilder.AppendDomain(userDomain)
                                .AppendSubDirectory(URLSubdirectory.FromString(userAddress))
                                .AppendSubDirectory(URLSubdirectory.FromString($"images?limit={limit}&offset={offset}&compact=true"))
                                .Build();

            urlBuilder.Clear();

            CameraReelResponsesCompact responseData = await webRequestController
                                                           .SignedFetchGetAsync(url, string.Empty, ReportCategory.CAMERA_REEL)
                                                           .CreateFromJsonAsync<CameraReelResponsesCompact>(WRJsonParser.Unity, ct);

            return responseData;
        }

        public async UniTask<CameraReelResponsesCompact> GetCompactPlaceScreenshotsAsync(string placeId, int limit, int offset, CancellationToken ct)
        {
            var url = urlBuilder.AppendDomain(placesDomain)
                                .AppendSubDirectory(URLSubdirectory.FromString(placeId))
                                .AppendSubDirectory(URLSubdirectory.FromString($"images?limit={limit}&offset={offset}"))
                                .Build();

            urlBuilder.Clear();

            CameraReelResponsesCompact responseData = await webRequestController
                                                           .SignedFetchGetAsync(url, string.Empty, ReportCategory.CAMERA_REEL)
                                                           .CreateFromJsonAsync<CameraReelResponsesCompact>(WRJsonParser.Unity, ct);

            return responseData;
        }

        public async UniTask<CameraReelResponsesCompact> UnsignedGetCompactScreenshotsAsync(string userAddress, int limit, int offset, CancellationToken ct)
        {
            var url = urlBuilder.AppendDomain(userDomain)
                                .AppendSubDirectory(URLSubdirectory.FromString(userAddress))
                                .AppendSubDirectory(URLSubdirectory.FromString($"images?limit={limit}&offset={offset}&compact=true"))
                                .Build();

            urlBuilder.Clear();

            CameraReelResponsesCompact responseData = await webRequestController
                                                           .GetAsync(url, ReportCategory.CAMERA_REEL)
                                                           .CreateFromJsonAsync<CameraReelResponsesCompact>(WRJsonParser.Unity, ct);

            return responseData;
        }

        public async UniTask<CameraReelStorageResponse> DeleteScreenshotAsync(string uuid, CancellationToken ct)
        {
            var url = urlBuilder.AppendDomain(imageDomain)
                                .AppendSubDirectory(URLSubdirectory.FromString(uuid))
                                .Build();

            urlBuilder.Clear();

            CameraReelStorageResponse responseData = await webRequestController
                                                          .SignedFetchDeleteAsync(url, string.Empty, ReportCategory.CAMERA_REEL)
                                                          .CreateFromJsonAsync<CameraReelStorageResponse>(WRJsonParser.Unity, ct);

            return responseData;
        }

        public async UniTask UpdateScreenshotVisibilityAsync(string uuid, bool isPublic, CancellationToken ct)
        {
            var url = urlBuilder.AppendDomain(imageDomain)
                                .AppendSubDirectory(URLSubdirectory.FromString(uuid))
                                .AppendSubDirectory(URLSubdirectory.FromString("visibility"))
                                .Build();

            urlBuilder.Clear();

            await webRequestController
                 .SignedFetchPatchAsync(url, GenericUploadArguments.CreateJson($"{{\"is_public\": {isPublic.ToString().ToLower()}}}"), string.Empty, ReportCategory.CAMERA_REEL)
                 .SendAndForgetAsync(ct);
        }

        public async UniTask<CameraReelUploadResponse> UploadScreenshotAsync(byte[] image, ScreenshotMetadata metadata, CancellationToken ct)
        {
            Uri url = urlBuilder.AppendDomain(imageDomain).Build();
            urlBuilder.Clear();

            var formData = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("image", image, $"{metadata.dateTime}.jpg", "image/jpeg"),
                new MultipartFormDataSection("metadata", JsonUtility.ToJson(metadata)),
            };

            CameraReelUploadResponse responseData = await webRequestController
                                                         .SignedFetchPostAsync(url, GenericUploadArguments.CreateMultipartForm(formData), string.Empty, ReportCategory.CAMERA_REEL)
                                                         .CreateFromJsonAsync<CameraReelUploadResponse>(WRJsonParser.Unity, ct);

            return responseData;
        }
    }
}
