using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System.Collections.Generic;
using System.Threading;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class PagedCameraReelManager
    {
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly PagedCameraReelManagerParameters parameters;
        private readonly int pageSize;
        private readonly int totalImages;

        private int currentOffset;
        private int currentLoadedImages;

        public bool AllImagesLoaded { get; private set; }
        public List<CameraReelResponseCompact> AllOrderedResponses { get; private set; } = new (32);

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

        public async UniTask FetchNextPageAsync(CancellationToken ct)
        {
            CameraReelResponsesCompact response = await FetchResponseAsync(ct);

            currentOffset += pageSize;
            currentLoadedImages += response.images.Count;

            // A short page ends the gallery even when the storage count is stale, so paging cannot spin on empty responses.
            AllImagesLoaded = currentLoadedImages >= totalImages || response.images.Count < pageSize;
            AllOrderedResponses.AddRange(response.images);
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

        public bool RemoveReelId(string reelId)
        {
            for (var i = 0; i < AllOrderedResponses.Count; i++)
                if (AllOrderedResponses[i].id == reelId)
                {
                    AllOrderedResponses.RemoveAt(i);
                    return true;
                }

            return false;
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
