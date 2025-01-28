using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using DCL.WebRequests.PartialDownload;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Buffers;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.Common.Systems
{
    public abstract class PartialDownloadSystemBase<TData, TIntention> : LoadSystemBase<TData, TIntention>
        where TIntention: struct, ILoadingIntention
    {
        private readonly IWebRequestController webRequestController;
        private readonly ArrayPool<byte> buffersPool;

        protected PartialDownloadSystemBase(
            World world,
            IStreamableCache<TData, TIntention> cache,
            IWebRequestController webRequestController,
            ArrayPool<byte> buffersPool,
            IDiskCache<TData> diskCache = null)
            : base(world, cache, diskCache)
        {
            this.webRequestController = webRequestController;
            this.buffersPool = buffersPool;
        }

        protected override async UniTask<StreamableLoadingResult<TData>> FlowInternalAsync(TIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            PartialLoadingState partialState = default;
            PartialDownloadingData chunkData;

            if (state.PartialDownloadingData == null)
            {
                // If the downloading has not started yet, create the first chunk data
                chunkData = new PartialDownloadingData(0, PartialDownloadingData.CHUNK_SIZE);
            }
            else
            {
                // If the downloading has already started, get the next chunk data
                partialState = state.PartialDownloadingData.Value;
                chunkData = new PartialDownloadingData(partialState.NextRangeStart,
                    Mathf.Min(partialState.FullFileSize - 1, partialState.NextRangeStart + PartialDownloadingData.CHUNK_SIZE));
            }

            try
            {
                await UniTask.SwitchToMainThread();
                chunkData = await webRequestController.GetPartialAsync(
                    intention.CommonArguments,
                    ct,
                    reportData: ReportCategory.PARTIAL_LOADING,
                    ref chunkData,
                    buffersPool,
                    headersInfo: new WebRequestHeadersInfo().WithRange(chunkData.RangeStart, chunkData.RangeEnd));

                //If this is the first chunk, we need to create the full data stream
                if (state.PartialDownloadingData == null)
                    partialState = new PartialLoadingState(chunkData.FullFileSize);

                int finalBytesCount = chunkData.downloadedSize;

                if (chunkData.RangeEnd > chunkData.FullFileSize)
                    finalBytesCount = chunkData.FullFileSize - chunkData.RangeStart;

                // Write the downloaded data to the full data stream by starting from the last range start
                partialState.AppendData(chunkData.DataBuffer.AsMemory()[..finalBytesCount]);

                state.SetChunkData(partialState);

                // Check if the download is complete
                if (partialState.FullyDownloaded)
                    return await ProcessCompletedData(state, intention, partition, ct);

                return default;
            }
            finally
            {
                if(chunkData.DataBuffer != null)
                    buffersPool.Return(chunkData.DataBuffer);
            }
        }

        protected abstract UniTask<StreamableLoadingResult<TData>> ProcessCompletedData(StreamableLoadingState state, TIntention intention, IPartitionComponent partition, CancellationToken ct);
    }
}
