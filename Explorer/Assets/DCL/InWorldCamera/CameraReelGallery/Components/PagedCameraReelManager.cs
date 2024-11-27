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
        public List<CameraReelResponseCompact> AllOrderedResponses { get; private set; } = new ();

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
    }

}
