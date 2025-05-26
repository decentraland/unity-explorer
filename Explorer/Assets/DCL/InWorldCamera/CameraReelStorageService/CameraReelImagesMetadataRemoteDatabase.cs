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
        private readonly URLDomain communityDomain;

        public CameraReelImagesMetadataRemoteDatabase(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;

            imageDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.CameraReelImages));
            userDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.CameraReelUsers));
            placesDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.CameraReelPlaces));
            communityDomain = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.CameraReelCommunity));
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

        public async UniTask<CameraReelStorageResponse> UnsignedGetStorageInfoAsync(string userAddress, CancellationToken ct)
        {
            URLAddress url = urlBuilder.AppendDomain(userDomain)
                                       .AppendSubDirectory(URLSubdirectory.FromString(userAddress))
                                       .Build();

            urlBuilder.Clear();

            CameraReelStorageResponse responseData = await webRequestController
                                                          .GetAsync(url, ct, ReportCategory.CAMERA_REEL)
                                                          .CreateFromJson<CameraReelStorageResponse>(WRJsonParser.Unity);

            return responseData;
        }

        public async UniTask<CameraReelResponse> GetScreenshotsMetadataAsync(string uuid, CancellationToken ct)
        {
            URLAddress url = urlBuilder.AppendDomain(imageDomain)
                                       .AppendSubDirectory(URLSubdirectory.FromString(uuid))
                                       .AppendSubDirectory(URLSubdirectory.FromString("metadata"))
                                       .Build();

            urlBuilder.Clear();

            CameraReelResponse responseData = await webRequestController
                                                   .SignedFetchGetAsync(url, string.Empty, ct)
                                                   .CreateFromJson<CameraReelResponse>(WRJsonParser.Unity);

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

        public async UniTask<CameraReelResponsesCompact> GetCompactScreenshotsAsync(string userAddress, int limit, int offset, CancellationToken ct)
        {
            URLAddress url = urlBuilder.AppendDomain(userDomain)
                                       .AppendSubDirectory(URLSubdirectory.FromString(userAddress))
                                       .AppendSubDirectory(URLSubdirectory.FromString($"images?limit={limit}&offset={offset}&compact=true"))
                                       .Build();

            urlBuilder.Clear();

            CameraReelResponsesCompact responseData = await webRequestController
                                                           .SignedFetchGetAsync(url, string.Empty, ct)
                                                           .CreateFromJson<CameraReelResponsesCompact>(WRJsonParser.Unity);

            return responseData;
        }

        public async UniTask<CameraReelResponsesCompact> GetCompactPlaceScreenshotsAsync(string placeId, int limit, int offset, CancellationToken ct)
        {
            URLAddress url = urlBuilder.AppendDomain(placesDomain)
                                       .AppendSubDirectory(URLSubdirectory.FromString(placeId))
                                       .AppendSubDirectory(URLSubdirectory.FromString($"images?limit={limit}&offset={offset}"))
                                       .Build();

            urlBuilder.Clear();

            CameraReelResponsesCompact responseData = await webRequestController
                                                           .SignedFetchGetAsync(url, string.Empty, ct)
                                                           .CreateFromJson<CameraReelResponsesCompact>(WRJsonParser.Unity);

            return responseData;
        }

        public async UniTask<CameraReelResponsesCompact> GetCompactCommunityScreenshotsAsync(string[] placeIds, int limit, int offset, CancellationToken ct)
        {
            // URLAddress url = urlBuilder.AppendDomain(communityDomain)
            //                            .AppendSubDirectory(URLSubdirectory.FromString($"images?limit={limit}&offset={offset}"))
            //                            .Build();
            // //TODO: Add placeIds to the request body
            //
            // urlBuilder.Clear();
            //
            // CameraReelResponsesCompact responseData = await webRequestController
            //                                                .SignedFetchPostAsync(url,  string.Empty, ct)
            //                                                .CreateFromJson<CameraReelResponsesCompact>(WRJsonParser.Unity);

            CameraReelResponsesCompact responseData = new CameraReelResponsesCompact
                {
                    currentImages = 15,
                    maxImages = 15,
                    images = new List<CameraReelResponseCompact>()
                };

            for (int i = 0; i < limit; i++)
                responseData.images.Add(new CameraReelResponseCompact()
                {
                    id = Guid.NewGuid().ToString(),
                    thumbnailUrl = "https://cdn.britannica.com/07/183407-050-C35648B5/Chicken.jpg",
                    isPublic = true,
                    dateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
                });

            return responseData;
        }

        public async UniTask<CameraReelResponsesCompact> UnsignedGetCompactScreenshotsAsync(string userAddress, int limit, int offset, CancellationToken ct)
        {
            URLAddress url = urlBuilder.AppendDomain(userDomain)
                                       .AppendSubDirectory(URLSubdirectory.FromString(userAddress))
                                       .AppendSubDirectory(URLSubdirectory.FromString($"images?limit={limit}&offset={offset}&compact=true"))
                                       .Build();

            urlBuilder.Clear();

            CameraReelResponsesCompact responseData = await webRequestController
                                                           .GetAsync(url, ct, ReportCategory.CAMERA_REEL)
                                                           .CreateFromJson<CameraReelResponsesCompact>(WRJsonParser.Unity);

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
