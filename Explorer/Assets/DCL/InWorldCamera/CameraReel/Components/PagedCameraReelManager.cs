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
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly int pageSize;
        private readonly Dictionary<DateTime, List<CameraReelResponse>> elements = new ();
        private readonly List<DateTime> buckets = new ();
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private int currentOffset;
        private int fetchedImages;
        private bool allImagesFetched;

        public PagedCameraReelManager(
            ICameraReelStorageService cameraReelStorageService,
            IWeb3IdentityCache web3IdentityCache,
            int pageSize)
        {
            this.cameraReelStorageService = cameraReelStorageService;
            this.web3IdentityCache = web3IdentityCache;
            this.pageSize = pageSize;
        }

        private async UniTask FetchNextPage(CancellationToken ct)
        {
            CameraReelResponses cameraReelResponses = await cameraReelStorageService.GetScreenshotGalleryAsync(web3IdentityCache.Identity.Address, pageSize, currentOffset, ct);
            currentOffset += pageSize;
            fetchedImages += cameraReelResponses.images.Count;

            allImagesFetched = fetchedImages == cameraReelResponses.currentImages;

            for (int i = 0; i < cameraReelResponses.images.Count; i++)
            {
                DateTime imageBucket = GetImageBucket(cameraReelResponses.images[i]);

                if (!elements.ContainsKey(imageBucket))
                {
                    elements[imageBucket] = new List<CameraReelResponse>();
                    buckets.Add(imageBucket);
                }

                elements[imageBucket].Add(cameraReelResponses.images[i]);
            }
        }

        public int GetBucketCount() =>
            buckets.Count;

        public async UniTask Initialize(CancellationToken ct)
        {
            currentOffset = 0;
            fetchedImages = 0;
            allImagesFetched = false;

            while (buckets.Count <= 1 && !allImagesFetched)
                await FetchNextPage(ct);
        }

        public (DateTime, List<CameraReelResponse>) GetBucket(int index)
        {
            if (index == Math.Max(buckets.Count - 2, 0) && !allImagesFetched)
                FetchNextPage(cancellationTokenSource.Token).Forget();

            return (buckets[index], elements[buckets[index]]);
        }

        public void Flush()
        {
            currentOffset = 0;
            fetchedImages = 0;
            allImagesFetched = false;
            elements.Clear();
            buckets.Clear();
            cancellationTokenSource.SafeCancelAndDispose();
        }

        private DateTime GetImageBucket(CameraReelResponse image)
        {
            DateTime actualDateTime = !long.TryParse(image.metadata.dateTime, out long unixTimestamp) ? new DateTime() : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;
            return new DateTime(actualDateTime.Year, actualDateTime.Month, 1);
        }
    }

}
