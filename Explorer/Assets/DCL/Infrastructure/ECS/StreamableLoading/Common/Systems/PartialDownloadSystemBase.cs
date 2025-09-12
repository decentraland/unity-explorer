using Arch.Core;
using AssetManagement;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.Cacheables;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Buffers;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace ECS.StreamableLoading.Common.Systems
{
    public abstract class PartialDownloadSystemBase<TData, TIntention> : LoadSystemBase<TData, TIntention>
        where TIntention: struct, ILoadingIntention, IEquatable<TIntention>
    {
        private const string PARTIALS_FILES_EXTENSION = "part";

        private readonly IWebRequestController webRequestController;
        private readonly ArrayPool<byte> buffersPool;
        private readonly IDiskCache<PartialLoadingState> partialDiskCache;
        private readonly IDiskHashCompute<TIntention> diskHashCompute;

        protected PartialDownloadSystemBase(
            World world,
            IStreamableCache<TData, TIntention> cache,
            IWebRequestController webRequestController,
            ArrayPool<byte> buffersPool,
            IDiskCache<PartialLoadingState> partialDiskCache,
            IDiskHashCompute<TIntention> diskHashCompute)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.buffersPool = buffersPool;
            this.partialDiskCache = partialDiskCache;
            this.diskHashCompute = diskHashCompute;
        }

        protected override async UniTask<StreamableLoadingResult<TData>> FlowInternalAsync(TIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            PartialLoadingState partialState = default;
            HashKey? diskHashKey = null;

            try
            {
                // Currently Outgoing requests treat Different sources as different flows (so 2 flows can be launched for the same assets bundle intention, they are not mutually blocking)
                // it's not a problem for a non-partial flow as if EMBEDDED does not exist if will fail immediately and won't lead to the second loading flow of the same file,
                // However with partial downloading two flows end up with the same HashKey, and subsequently with the same valid file (because the file received from WEB could be already cached on DISK but now is requested from EMBEDDED) ,
                // it can lead to loading of the same file twice
                // The most proper solution would be to change OutgoingRequests to be agnostic to the CurrentSource, // TODO to be refactored later
                // Currently, the solution is to launch disk cache for WEB only as it does not make sense to read from disk cache if the file is already available in EMBEDDED assets (on disk)

                bool isQualifiedForDiskCache = intention.IsQualifiedForDiskCache();

                if (isQualifiedForDiskCache)
                {
                    diskHashKey = diskHashCompute.ComputeHash(intention);

                    EnumResult<Option<PartialLoadingState>, TaskError> cachedPartial = await partialDiskCache.ContentAsync(diskHashKey.Value, PARTIALS_FILES_EXTENSION, ct);

                    if (cachedPartial is { Success: true, Value: { Has: true } })
                    {
                        PartialLoadingState cachedState = cachedPartial.Value.Value;
                        state.SetChunkData(cachedState);
                        // If the cached data is complete, process it directly
                        if (cachedState.IsFileFullyDownloaded)
                            return await ProcessCompletedDataAsync(state, intention, partition, ct);
                    }
                    else

                        // If no cache or incomplete cached data, proceed with normal flow
                        partialState = default(PartialLoadingState);
                }

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

                    if (isQualifiedForDiskCache)
                        partialDiskCache.PutAsync(diskHashKey!.Value, PARTIALS_FILES_EXTENSION, partialState, ct).Forget();

                    state.SetChunkData(partialState);

                    // Check if the download is complete
                    if (partialState.FullyDownloaded)
                        return await ProcessCompletedDataAsync(state, intention, partition, ct);

                    return default(StreamableLoadingResult<TData>);
                }
                //This catch is a workaround for the loading breaking bug caused by multiple scenes having same asset hash
                //but with different file sizes, it won't load the asset but won't block the loading
                catch (UnityWebRequestException e) when (e.ResponseCode == 416)
                {
                    return new StreamableLoadingResult<TData>(new ReportData(), e);
                }
                finally
                {
                    if (downloadedData.DestinationArray != null)
                        buffersPool.Return(downloadedData.DestinationArray);
                }
            }
            finally { diskHashKey?.Dispose(); }
        }

        protected abstract UniTask<StreamableLoadingResult<TData>> ProcessCompletedDataAsync(StreamableLoadingState state, TIntention intention, IPartitionComponent partition, CancellationToken ct);
    }
}
