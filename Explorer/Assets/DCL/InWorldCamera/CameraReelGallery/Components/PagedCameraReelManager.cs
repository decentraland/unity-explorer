using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
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

        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly string walletAddress;
        private readonly int pageSize;
        private readonly int totalImages;

        private int currentOffset;
        private int currentLoadedImages;

        public PagedCameraReelManager(
            ICameraReelStorageService cameraReelStorageService,
            string wallet,
            int totalImages,
            int pageSize)
        {
            this.cameraReelStorageService = cameraReelStorageService;
            this.walletAddress = wallet;
            this.totalImages = totalImages;
            this.pageSize = pageSize;
        }


        public async UniTask<Dictionary<DateTime, List<CameraReelResponseCompact>>> FetchNextPageAsync(
            ListObjectPool<CameraReelResponseCompact> listPool,
            CancellationToken ct)
        {
            CameraReelResponsesCompact response = await cameraReelStorageService.GetCompactScreenshotGalleryAsync(walletAddress, pageSize, currentOffset, ct);
            currentOffset += pageSize;

            currentLoadedImages += response.images.Count;
            AllImagesLoaded = currentLoadedImages == totalImages;

            Dictionary<DateTime, List<CameraReelResponseCompact>> elements = DictionaryPool<DateTime, List<CameraReelResponseCompact>>.Get();
            for (int i = 0; i < response.images.Count; i++)
            {
                DateTime imageBucket = GetImageDateTime(response.images[i]);

                if (!elements.ContainsKey(imageBucket))
                    elements[imageBucket] = listPool.Get();

                elements[imageBucket].Add(response.images[i]);
            }

            return elements;
        }

        public static DateTime GetImageDateTime(CameraReelResponseCompact image) =>
            GetDateTimeFromString(image.dateTime);

        public static DateTime GetDateTimeFromString(string epochString)
        {
            DateTime actualDateTime = !long.TryParse(epochString, out long unixTimestamp) ? new DateTime() : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;
            return new DateTime(actualDateTime.Year, actualDateTime.Month, 1, 0, 0, 0, 0);
        }
    }

}
