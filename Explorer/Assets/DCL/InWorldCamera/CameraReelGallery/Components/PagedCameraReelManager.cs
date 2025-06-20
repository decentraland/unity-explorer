using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.ReelActions;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class PagedCameraReelManager
    {
        public bool AllImagesLoaded { get; private set; }
        public List<CameraReelResponseCompact> AllOrderedResponses { get; private set; } = new (32);

        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly PagedCameraReelManagerParameters parameters;
        private readonly int pageSize;
        private readonly int totalImages;

        private int currentOffset;
        private int currentLoadedImages;

        public PagedCameraReelManager(
            ICameraReelStorageService cameraReelStorageService,
            PagedCameraReelManagerParameters parameters,
            int totalImages,
            int pageSize)
        {
            this.cameraReelStorageService = cameraReelStorageService;
            this.parameters = parameters;
            this.totalImages = totalImages;
            this.pageSize = pageSize;
        }

        public async UniTask<Dictionary<DateTime, List<CameraReelResponseCompact>>> FetchNextPageAsync(
            ListObjectPool<CameraReelResponseCompact> listPool,
            CancellationToken ct)
        {
            CameraReelResponsesCompact response = await FetchResponseAsync(ct);

            return ProcessResponse(response, listPool);
        }

        private async UniTask<CameraReelResponsesCompact> FetchResponseAsync(CancellationToken ct)
        {
            if (parameters.PlaceIds != null)
                return await cameraReelStorageService.GetCompactPlacesScreenshotGalleryAsync(parameters.PlaceIds, pageSize, currentOffset, ct);

            if (parameters.PlaceId != null)
                return await cameraReelStorageService.GetCompactPlaceScreenshotGalleryAsync(parameters.PlaceId, pageSize, currentOffset, ct);

            if (parameters.UseSignedRequest.HasValue && parameters.UseSignedRequest.Value)
                return await cameraReelStorageService.GetCompactScreenshotGalleryAsync(parameters.WalletAddress, pageSize, currentOffset, ct);

            return await cameraReelStorageService.UnsignedGetCompactScreenshotGalleryAsync(parameters.WalletAddress, pageSize, currentOffset, ct);
        }

        private Dictionary<DateTime, List<CameraReelResponseCompact>> ProcessResponse(CameraReelResponsesCompact response, ListObjectPool<CameraReelResponseCompact> listPool)
        {
            currentOffset += pageSize;

            currentLoadedImages += response.images.Count;
            AllImagesLoaded = currentLoadedImages == totalImages;
            AllOrderedResponses.AddRange(response.images);

            Dictionary<DateTime, List<CameraReelResponseCompact>> elements = DictionaryPool<DateTime, List<CameraReelResponseCompact>>.Get();
            for (int i = 0; i < response.images.Count; i++)
            {
                DateTime imageBucket = ReelUtility.GetImageDateTime(response.images[i]);

                if (!elements.ContainsKey(imageBucket))
                    elements[imageBucket] = listPool.Get();

                elements[imageBucket].Add(response.images[i]);
            }

            return elements;
        }

        public void RemoveReelId(string reelId)
        {
            for (int i = 0; i < AllOrderedResponses.Count; i++)
                if (AllOrderedResponses[i].id == reelId)
                {
                    AllOrderedResponses.RemoveAt(i);
                    break;
                }
        }
    }

    public struct PagedCameraReelManagerParameters
    {
        public readonly string? WalletAddress;
        public readonly bool? UseSignedRequest;
        public readonly string? PlaceId;
        public readonly string[]? PlaceIds;

        public PagedCameraReelManagerParameters(string walletAddress, bool useSignedRequest)
        {
            this.WalletAddress = walletAddress;
            this.UseSignedRequest = useSignedRequest;
            this.PlaceId = null;
            this.PlaceIds = null;
        }

        public PagedCameraReelManagerParameters(string placeId)
        {
            this.WalletAddress = null;
            this.UseSignedRequest = null;
            this.PlaceId = placeId;
            this.PlaceIds = null;
        }

        public PagedCameraReelManagerParameters(string[] placeIds)
        {
            this.WalletAddress = null;
            this.UseSignedRequest = null;
            this.PlaceId = null;
            this.PlaceIds = placeIds;
        }
    }
}
