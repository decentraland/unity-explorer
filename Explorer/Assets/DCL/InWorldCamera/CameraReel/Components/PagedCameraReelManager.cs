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
        private CancellationTokenSource cancellationTokenSource = new();

        public PagedCameraReelManager(
            ICameraReelStorageService cameraReelStorageService,
            IWeb3IdentityCache web3IdentityCache,
            int pageSize)
        {
            this.cameraReelStorageService = cameraReelStorageService;
            this.web3IdentityCache = web3IdentityCache;
            this.pageSize = pageSize;
        }

        public int GetBucketCount() =>
            buckets.Count;

        public async UniTask Initialize(int totalScreenshot, CancellationToken ct)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();

            int parallelNumber = (int)MathF.Ceiling(totalScreenshot * 1f / pageSize);

            UniTask<CameraReelResponses>[] tasks = new UniTask<CameraReelResponses>[parallelNumber];

            for (int i = 0; i < parallelNumber; i++)
                tasks[i] = cameraReelStorageService.GetScreenshotGalleryAsync(web3IdentityCache.Identity.Address, pageSize, pageSize * i, ct);

            CameraReelResponses[] taskResults = await UniTask.WhenAll(tasks);

            for (int j = 0; j < taskResults.Length; j++)
                for (int i = 0; i < taskResults[j].images.Count; i++)
                {
                    DateTime imageBucket = GetImageBucket(taskResults[j].images[i]);

                    if (!elements.ContainsKey(imageBucket))
                    {
                        elements[imageBucket] = new List<CameraReelResponse>();
                        buckets.Add(imageBucket);
                    }

                    elements[imageBucket].Add(taskResults[j].images[i]);
                }
        }

        public (DateTime, List<CameraReelResponse>) GetBucket(int index) =>
            (buckets[index], elements[buckets[index]]);

        public void Flush()
        {
            elements.Clear();
            buckets.Clear();
            cancellationTokenSource.SafeCancelAndDispose();
        }

        private DateTime GetImageBucket(CameraReelResponse image)
        {
            DateTime actualDateTime = !long.TryParse(image.metadata.dateTime, out long unixTimestamp) ? new DateTime() : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;
            return new DateTime(actualDateTime.Year, actualDateTime.Month, 1, 0, 0, 0, 0);
        }
    }

}
