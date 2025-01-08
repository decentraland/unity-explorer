using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using DCL.WebRequests.PartialDownload;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Buffers;
using System.Threading;

namespace DCL.PartialDownloading.Systems
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.PARTIAL_LOADING)]
    public partial class PartialLoadSystem : LoadSystemBase<FullDownloadedData, GetPartialIntention>
    {
        private readonly IWebRequestController webRequestController;
        private const int CHUNK_SIZE = 1024 * 1024;
        private readonly ArrayPool<byte> arrayPool;

        internal PartialLoadSystem(
            World world,
            IStreamableCache<FullDownloadedData, GetPartialIntention> cache,
            IWebRequestController webRequestController, ArrayPool<byte> arrayPool) : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.arrayPool = arrayPool;
        }

        protected override async UniTask<StreamableLoadingResult<FullDownloadedData>> FlowInternalAsync(GetPartialIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            byte[] partialDownloadBuffer = arrayPool.Rent(CHUNK_SIZE);

            //Create the new partial downloading data
            PartialDownloadingData partialDownloadingData = new PartialDownloadingData(partialDownloadBuffer, 0, CHUNK_SIZE);

            //If not then perform the first request
            await webRequestController.GetPartialAsync(intention.CommonArguments, ct, reportData: ReportCategory.PARTIAL_LOADING, partialDownloadingData);

            //Allocate the full data buffer based on full file size
            byte[] fullDataBuffer = arrayPool.Rent(partialDownloadingData.FullFileSize);

            //Copy the first chunk of data to the full data buffer
            Buffer.BlockCopy(partialDownloadingData.DataBuffer, 0, fullDataBuffer, 0, partialDownloadingData.DataBuffer.Length);

            //download next chunks of data when
            if(partialDownloadingData.RangeStart < partialDownloadingData.FullFileSize)
            {
                partialDownloadingData.RangeStart += CHUNK_SIZE;
                partialDownloadingData.RangeEnd =  partialDownloadingData.RangeStart + CHUNK_SIZE;

                await webRequestController.GetPartialAsync(intention.CommonArguments, ct, reportData: ReportCategory.PARTIAL_LOADING, partialDownloadingData);

                int finalBytesCount = partialDownloadingData.DataBuffer.Length;
                if (partialDownloadingData.RangeEnd > partialDownloadingData.FullFileSize)
                    finalBytesCount = partialDownloadingData.FullFileSize - partialDownloadingData.RangeStart;

                Buffer.BlockCopy(partialDownloadingData.DataBuffer, 0, fullDataBuffer, partialDownloadingData.RangeStart, finalBytesCount);
            }

            //Create full downloaded data
            FullDownloadedData fullDownloadedData = new FullDownloadedData(fullDataBuffer);
            arrayPool.Return(partialDownloadBuffer, true);

            return new StreamableLoadingResult<FullDownloadedData>(fullDownloadedData);
        }
    }
}
