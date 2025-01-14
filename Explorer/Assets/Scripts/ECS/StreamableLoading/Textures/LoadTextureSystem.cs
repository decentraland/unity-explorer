using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using DCL.Diagnostics;
using DCL.WebRequests.PartialDownload;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadTextureSystem : LoadSystemBase<Texture2DData, GetTextureIntention>
    {
        //private readonly ArrayPool<byte> arrayPool = ArrayPool<byte>.Create();
        private readonly IWebRequestController webRequestController;

        internal LoadTextureSystem(World world, IStreamableCache<Texture2DData, GetTextureIntention> cache, IWebRequestController webRequestController) : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetTextureIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            PartialLoadingState partialState = default;
            PartialDownloadingData chunkData;

            // TODO pooling
            var partialDownloadBuffer = new byte[PartialDownloadingData.CHUNK_SIZE];

            // If the downloading has not started yet
            if (state.PartialDownloadingData == null) { chunkData = new PartialDownloadingData(partialDownloadBuffer, 0, PartialDownloadingData.CHUNK_SIZE); }
            else
            {
                partialState = state.PartialDownloadingData.Value;

                // Continue downloading
                chunkData = new PartialDownloadingData(partialDownloadBuffer, partialState.NextRangeStart, Mathf.Min(partialState.FullFileSize - 1, partialState.NextRangeStart + PartialDownloadingData.CHUNK_SIZE));
            }


            /*TODO add the proper flow to do the first request, handle the partial result if supports partial download and
             handle direct result creation if partial is not supported or file is smaller than chunk*/

            // Execute a single chunk
            // it should return another structure for clarity
            chunkData = await webRequestController.GetPartialAsync(
                intention.CommonArguments,
                ct,
                reportData: ReportCategory.PARTIAL_LOADING,
                chunkData,
                headersInfo: new WebRequestHeadersInfo().WithRange(chunkData.RangeStart, chunkData.RangeEnd));

            // if it was the first request create the state
            if (state.PartialDownloadingData == null)
            {
                //Allocate the full data buffer based on full file size
                //Temporary solution atm, will change to a Memory or Stream
                var fullDataBuffer = new byte[chunkData.FullFileSize]; //arrayPool.Rent(partialDownloadingData.FullFileSize);

                partialState = new PartialLoadingState(fullDataBuffer, chunkData.FullFileSize);
            }

            int finalBytesCount = chunkData.DataBuffer.Length;

            if (chunkData.RangeEnd > chunkData.FullFileSize)
                finalBytesCount = chunkData.FullFileSize - chunkData.RangeStart;

            Buffer.BlockCopy(chunkData.DataBuffer, 0, partialState.FullData!, chunkData.RangeStart, finalBytesCount);

            partialState.NextRangeStart = chunkData.RangeEnd + 1;

            state.SetChunkCompleted(partialState);

            // If the file is fully loaded produce the result
            if (partialState.FullyDownloaded)
            {
                var texture = new Texture2D(1, 1);
                texture.LoadImage(partialState.FullData);
                return new StreamableLoadingResult<Texture2DData>(new Texture2DData(texture));
            }

            // Spin
            return default(StreamableLoadingResult<Texture2DData>);
        }
    }
}
