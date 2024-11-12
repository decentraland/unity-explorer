using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class PagedCameraReelManager
    {
        public bool AllImagesLoaded { get; private set; } = false;

        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly string walletAddress;
        private readonly int pageSize;

        private int currentOffset = 0;
        private int currentLoadedImages = 0;

        public PagedCameraReelManager(
            ICameraReelStorageService cameraReelStorageService,
            string wallet,
            int pageSize)
        {
            this.cameraReelStorageService = cameraReelStorageService;
            this.walletAddress = wallet;
            this.pageSize = pageSize;
        }


        public async UniTask<Dictionary<DateTime, List<CameraReelResponse>>> FetchNextPage(CancellationToken ct)
        {
            CameraReelResponses response = await cameraReelStorageService.GetScreenshotGalleryAsync(walletAddress, pageSize, currentOffset, ct);
            currentOffset += pageSize;

            currentLoadedImages += response.images.Count;
            AllImagesLoaded = currentLoadedImages == response.currentImages;

            Dictionary<DateTime, List<CameraReelResponse>> elements = new ();
            for (int i = 0; i < response.images.Count; i++)
            {
                DateTime imageBucket = GetImageDateTime(response.images[i]);

                if (!elements.ContainsKey(imageBucket))
                    elements[imageBucket] = new List<CameraReelResponse>();

                elements[imageBucket].Add(response.images[i]);
            }

            return elements;
        }

        public static DateTime GetImageDateTime(CameraReelResponse image)
        {
            DateTime actualDateTime = !long.TryParse(image.metadata.dateTime, out long unixTimestamp) ? new DateTime() : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;
            return new DateTime(actualDateTime.Year, actualDateTime.Month, 1, 0, 0, 0, 0);
        }
    }

}
