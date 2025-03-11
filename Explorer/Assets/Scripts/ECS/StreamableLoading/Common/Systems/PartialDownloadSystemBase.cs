using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using DCL.WebRequests;
using DCL.WebRequests.PartialDownload;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.Cacheables;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Buffers;
using System.Threading;
using Utility.Multithreading;
using Utility.Types;

namespace ECS.StreamableLoading.Common.Systems
{
    public abstract class PartialDownloadSystemBase<TData, TIntention> : LoadSystemBase<TData, TIntention>
        where TIntention: struct, ILoadingIntention
    {
        private const string PARTIALS_FILES_EXTENSION = "part";

        private readonly IWebRequestController webRequestController;
        private readonly ArrayPool<byte> buffersPool;
        private readonly IPartialDiskCache partialDiskCache;
        private readonly IDiskHashCompute<TIntention> diskHashCompute;

        protected PartialDownloadSystemBase(
            World world,
            IStreamableCache<TData, TIntention> cache,
            IWebRequestController webRequestController,
            ArrayPool<byte> buffersPool,
            IPartialDiskCache partialDiskCache,
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
            // Currently Outgoing requests treat Different sources as different flows (so 2 flows can be launched for the same assets bundle intention, they are not mutually blocking)
            // it's not a problem for a non-partial flow as if EMBEDDED does not exist if will fail immediately and won't lead to the second loading flow of the same file,
            // However with partial downloading two flows end up with the same HashKey, and subsequently with the same valid file (because the file received from WEB could be already cached on DISK but now is requested from EMBEDDED) ,
            // it can lead to loading of the same file twice
            // The most proper solution would be to change OutgoingRequests to be agnostic to the CurrentSource, // TODO to be refactored later
            // Currently, the solution is to launch disk cache for WEB only as it does not make sense to read from disk cache if the file is already available in EMBEDDED assets (on disk)

            // bool isQualifiedForDiskCache = intention.IsQualifiedForDiskCache();
            //
            // if (isQualifiedForDiskCache)
            // make separate flow later for files which are stored on disk
            {
                using HashKey diskHashKey = diskHashCompute.ComputeHash(intention);

                EnumResult<MutexSlim<PartialFile>, TaskError> cachedPartial = await partialDiskCache.PartialFileAsync(diskHashKey, PARTIALS_FILES_EXTENSION, ct);

                if (cachedPartial.Success == false)
                    throw new Exception($"Failed to get partial file from disk cache {intention}: {cachedPartial}");

                var file = cachedPartial.Value;
                PartialLoadingState cachedState = new PartialLoadingState(file);
                state.SetChunkData(cachedState);

                // If the cached data is complete, process it directly
                if (cachedState.IsFileFullyDownloaded)
                    return await ProcessCompletedDataAsync(state, intention, partition, ct);

                // If no cache or incomplete cached data, proceed with normal flow
            }

            PartialLoadingState partialState = default;

            if (state.PartialDownloadingData.HasValue)
                partialState = state.PartialDownloadingData.Value;

            try
            {
                await UniTask.SwitchToMainThread();

                await partialState
                     .PeekOwner()
                     .AccessAsync((webRequestController, intention, buffersPool, ct), LoadAsync, ct);

                state.SetChunkData(partialState);

                // Check if the download is complete
                if (partialState.IsFileFullyDownloaded)
                    return await ProcessCompletedDataAsync(state, intention, partition, ct);

                return default(StreamableLoadingResult<TData>);
            }

            //This catch is a workaround for the loading breaking bug caused by multiple scenes having same asset hash
            //but with different file sizes, it won't load the asset but won't block the loading
            catch (UnityWebRequestException e) when (e.ResponseCode == 416) { return new StreamableLoadingResult<TData>(new ReportData(), e); }
        }

        protected abstract UniTask<StreamableLoadingResult<TData>> ProcessCompletedDataAsync(StreamableLoadingState state, TIntention intention, IPartitionComponent partition, CancellationToken ct);

        private static async UniTask LoadAsync(PartialFile file, (IWebRequestController webRequestController, TIntention intention, ArrayPool<byte> bufferPool, CancellationToken token) tuple)
        {
            if (file.MetaData.IsFullyDownloaded)
                return;

            var webRequestController = tuple.webRequestController!;
            var buffersPool = tuple.bufferPool!;
            var intention = tuple.intention;
            var ct = tuple.token;
            PartialDownloadingRange chunkRange = file.NewPartialDownloadingRange();

            using PartialDownloadedData downloadedData = await webRequestController.GetPartialAsync(
                intention.CommonArguments,
                ct,
                reportData: ReportCategory.PARTIAL_LOADING,
                buffersPool,
                headersInfo: new WebRequestHeadersInfo().WithRange(chunkRange.RangeStart, chunkRange.RangeEnd));

            //If this is the first chunk, we need to create the full data stream
            await file.UpdateFullSizeIfRequiredAsync(downloadedData.FullFileSize);

            int finalBytesCount = downloadedData.DownloadedSize;

            if (chunkRange.RangeEnd > downloadedData.FullFileSize)
                finalBytesCount = downloadedData.FullFileSize - chunkRange.RangeStart;

            // Write the downloaded data to the full data stream by starting from the last range start
            await file.AppendDataAsync(downloadedData.DestinationArray!.AsMemory()[..finalBytesCount]);
        }
    }
}
