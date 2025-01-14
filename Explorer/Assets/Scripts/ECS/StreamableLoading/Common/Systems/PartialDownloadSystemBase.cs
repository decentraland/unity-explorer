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

        protected PartialDownloadSystemBase(World world, IStreamableCache<TData, TIntention> cache, IWebRequestController webRequestController)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<TData>> FlowInternalAsync(TIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            PartialLoadingState partialState = default;
            PartialDownloadingData chunkData;

            byte[] partialDownloadBuffer = ArrayPool<byte>.Shared!.Rent(PartialDownloadingData.CHUNK_SIZE)!;

            try
            {
                // If the downloading has not started yet
                if (state.PartialDownloadingData == null) { chunkData = new PartialDownloadingData(partialDownloadBuffer, 0, PartialDownloadingData.CHUNK_SIZE); }
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
                    var fullDataBuffer = new byte[chunkData.FullFileSize];
                    partialState = new PartialLoadingState(fullDataBuffer, chunkData.FullFileSize);
                }

                int finalBytesCount = chunkData.DataBuffer.Length;

                if (chunkData.RangeEnd > chunkData.FullFileSize)
                    finalBytesCount = chunkData.FullFileSize - chunkData.RangeStart;

                Buffer.BlockCopy(chunkData.DataBuffer, 0, partialState.FullData!, chunkData.RangeStart, finalBytesCount);
                partialState.NextRangeStart = chunkData.RangeEnd + 1;

                state.SetChunkCompleted(partialState);

                if (partialState.FullyDownloaded) { return ProcessCompletedData(partialState.FullData!); }

                return default;
            }
            finally { ArrayPool<byte>.Shared!.Return(partialDownloadBuffer); }
        }

        protected abstract StreamableLoadingResult<TData> ProcessCompletedData(byte[] completeData);
    }
}
