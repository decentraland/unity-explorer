using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using DCL.WebRequests.PartialDownload;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
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
            ArrayPool<byte> buffersPool)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.buffersPool = buffersPool;
        }

        protected override async UniTask<StreamableLoadingResult<TData>> FlowInternalAsync(TIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            PartialLoadingState partialState = default;
            PartialDownloadingData chunkData;

            byte[] partialDownloadBuffer = buffersPool.Rent(PartialDownloadingData.CHUNK_SIZE)!;

            try
            {
                // If the downloading has not started yet
                if (state.PartialDownloadingData == null)
                {
                    chunkData = new PartialDownloadingData(partialDownloadBuffer, 0, PartialDownloadingData.CHUNK_SIZE);
                }
                else
                {
                    partialState = state.PartialDownloadingData.Value;

                    chunkData = new PartialDownloadingData(partialDownloadBuffer, partialState.NextRangeStart,
                        Mathf.Min(partialState.FullFileSize - 1, partialState.NextRangeStart + PartialDownloadingData.CHUNK_SIZE));
                }

                chunkData = await webRequestController.GetPartialAsync(
                    intention.CommonArguments,
                    ct,
                    reportData: ReportCategory.PARTIAL_LOADING,
                    chunkData,
                    headersInfo: new WebRequestHeadersInfo().WithRange(chunkData.RangeStart, chunkData.RangeEnd));

                if (state.PartialDownloadingData == null)
                {
                    var fullDataMemory = new Memory<byte>(new byte[chunkData.FullFileSize]);
                    partialState = new PartialLoadingState(fullDataMemory, chunkData.FullFileSize);
                }

                int finalBytesCount = chunkData.DataBuffer.Length;

                if (chunkData.RangeEnd > chunkData.FullFileSize)
                    finalBytesCount = chunkData.FullFileSize - chunkData.RangeStart;

                chunkData.DataBuffer.AsMemory(0, finalBytesCount).CopyTo(partialState.FullData.Slice(chunkData.RangeStart, finalBytesCount));
                partialState.NextRangeStart = chunkData.RangeEnd + 1;

                if (partialState.FullyDownloaded)
                {
                    StreamableLoadingResult<TData> loadedResult = await ProcessCompletedData(partialState.FullData.ToArray(), intention, partition, ct, state);
                    state.SetChunkCompleted(partialState);
                    return loadedResult;
                }

                state.SetChunkCompleted(partialState);
                return default;
            }
            finally { buffersPool.Return(partialDownloadBuffer); }
        }

        protected abstract UniTask<StreamableLoadingResult<TData>> ProcessCompletedData(byte[] completeData, TIntention intention, IPartitionComponent partition, CancellationToken ct, StreamableLoadingState state);
    }
}
