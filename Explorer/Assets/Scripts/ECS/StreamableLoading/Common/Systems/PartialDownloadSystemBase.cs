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
using Utility.Types;

namespace ECS.StreamableLoading.Common.Systems
{
    public abstract class PartialDownloadSystemBase<TData, TIntention> : LoadSystemBase<TData, TIntention>
        where TIntention: struct, ILoadingIntention
    {
        private const string PARTIALS_FILES_EXTENSION = "part";

        private readonly IWebRequestController webRequestController;
        private readonly ArrayPool<byte> buffersPool;
        private IDiskCache<PartialLoadingState> partialDiskCache;

        protected PartialDownloadSystemBase(
            World world,
            IStreamableCache<TData, TIntention> cache,
            IWebRequestController webRequestController,
            ArrayPool<byte> buffersPool,
            IDiskCache<PartialLoadingState> partialDiskCache)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.buffersPool = buffersPool;
            this.partialDiskCache = partialDiskCache;
        }

        protected override async UniTask<StreamableLoadingResult<TData>> FlowInternalAsync(TIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            EnumResult<Option<PartialLoadingState>,TaskError> cachedPartial = await partialDiskCache.ContentAsync(HashKey.FromString(intention.CommonArguments.GetCacheableURL()), PARTIALS_FILES_EXTENSION, ct);

            if (cachedPartial.Success && cachedPartial.Value.Has)
            {
                var cachedState = cachedPartial.Value.Value;
                state.SetChunkData(cachedState);

                // If the cached data is complete, process it directly
                if (cachedState.FullyDownloaded)
                    return await ProcessCompletedData(state, intention, partition, ct);
            }

            // If no cache or incomplete cached data, proceed with normal flow
            PartialLoadingState partialState = cachedPartial.Success && cachedPartial.Value.Has ?
                cachedPartial.Value.Value : default;
            PartialDownloadingRange chunkRange;
            PartialDownloadedData downloadedData = default;

            if (state.PartialDownloadingData == null)
            {
                // If the downloading has not started yet, create the first chunk data
                chunkRange = new PartialDownloadingRange(0, PartialDownloadingRange.CHUNK_SIZE);
            }
            else
            {
                // If the downloading has already started, get the next chunk data
                partialState = state.PartialDownloadingData.Value;
                chunkRange = new PartialDownloadingRange(partialState.NextRangeStart,
                    Mathf.Min(partialState.FullFileSize - 1, partialState.NextRangeStart + PartialDownloadingRange.CHUNK_SIZE));
            }

            try
            {
                await UniTask.SwitchToMainThread();
                downloadedData = await webRequestController.GetPartialAsync(
                    intention.CommonArguments,
                    ct,
                    reportData: ReportCategory.PARTIAL_LOADING,
                    buffersPool,
                    headersInfo: new WebRequestHeadersInfo().WithRange(chunkRange.RangeStart, chunkRange.RangeEnd));

                //If this is the first chunk, we need to create the full data stream
                if (state.PartialDownloadingData == null)
                    partialState = new PartialLoadingState(downloadedData.FullFileSize);

                int finalBytesCount = downloadedData.DownloadedSize;

                if (chunkRange.RangeEnd > downloadedData.FullFileSize)
                    finalBytesCount = downloadedData.FullFileSize - chunkRange.RangeStart;

                // Write the downloaded data to the full data stream by starting from the last range start
                partialState.AppendData(downloadedData.DestinationArray.AsMemory()[..finalBytesCount]);

                partialDiskCache.PutAsync(HashKey.FromString(GetIntentionResourceStringFromUrl(intention)), PARTIALS_FILES_EXTENSION, partialState, ct).Forget();
                state.SetChunkData(partialState);

                // Check if the download is complete
                if (partialState.FullyDownloaded)
                    return await ProcessCompletedData(state, intention, partition, ct);

                return default;
            }
            finally
            {
                if(downloadedData.DestinationArray != null)
                    buffersPool.Return(downloadedData.DestinationArray);
            }
        }

        private string GetIntentionResourceStringFromUrl(TIntention intention) =>
            intention.CommonArguments.URL.Value.Split("/")[^1];

        protected abstract UniTask<StreamableLoadingResult<TData>> ProcessCompletedData(StreamableLoadingState state, TIntention intention, IPartitionComponent partition, CancellationToken ct);
    }
}
