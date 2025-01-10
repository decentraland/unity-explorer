using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utilities.Extensions;
using DCL.WebRequests.PartialDownload;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.Unity.Textures.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadTextureSystem : LoadSystemBase<Texture2DData, GetTextureIntention>
    {
        private const int CHUNK_SIZE = 1024 * 1024;
        private readonly ArrayPool<byte> arrayPool;
        private readonly IWebRequestController webRequestController;
        private readonly Dictionary<GetTextureIntention, int> currentRequests = new Dictionary<GetTextureIntention, int>();

        internal LoadTextureSystem(World world, IStreamableCache<Texture2DData, GetTextureIntention> cache, IWebRequestController webRequestController) : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetTextureIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct, EntityReference entity)
        {
            byte[] partialDownloadBuffer = arrayPool.Rent(CHUNK_SIZE);

            //Create the new partial downloading data
            PartialDownloadingData partialDownloadingData = new PartialDownloadingData(partialDownloadBuffer, 0, CHUNK_SIZE);

            //If not then perform the first request
            await webRequestController.GetPartialAsync(
                intention.CommonArguments,
                ct,
                reportData: ReportCategory.PARTIAL_LOADING,
                partialDownloadingData,
                headersInfo: new WebRequestHeadersInfo().WithRange(partialDownloadingData.RangeStart, partialDownloadingData.RangeEnd));

            currentRequests.Add(intention, 1);
            //Allocate the full data buffer based on full file size
            byte[] fullDataBuffer = arrayPool.Rent(partialDownloadingData.FullFileSize);

            //Copy the first chunk of data to the full data buffer
            Buffer.BlockCopy(partialDownloadingData.DataBuffer, 0, fullDataBuffer, 0, partialDownloadingData.DataBuffer.Length);

            //download next chunks of data when
            if(partialDownloadingData.RangeStart < partialDownloadingData.FullFileSize)
            {
                partialDownloadingData.RangeStart += CHUNK_SIZE;
                partialDownloadingData.RangeEnd =  partialDownloadingData.RangeStart + CHUNK_SIZE;

                await webRequestController.GetPartialAsync(
                    intention.CommonArguments,
                    ct,
                    reportData: ReportCategory.PARTIAL_LOADING,
                    partialDownloadingData,
                    headersInfo: new WebRequestHeadersInfo().WithRange(partialDownloadingData.RangeStart, partialDownloadingData.RangeEnd));

                int finalBytesCount = partialDownloadingData.DataBuffer.Length;
                if (partialDownloadingData.RangeEnd > partialDownloadingData.FullFileSize)
                    finalBytesCount = partialDownloadingData.FullFileSize - partialDownloadingData.RangeStart;

                Buffer.BlockCopy(partialDownloadingData.DataBuffer, 0, fullDataBuffer, partialDownloadingData.RangeStart, finalBytesCount);

                currentRequests[intention]++;
                StreamableLoadingState state = World!.Get<StreamableLoadingState>(entity);
                state.AcquiredBudget.Release();
                state.SetChunkCompleted();
                await UniTask.WaitUntil(() => state.Value == StreamableLoadingState.Status.ProcessNextChunk);
            }

            arrayPool.Return(partialDownloadBuffer, true);

            var texture = new Texture2D(1, 1);
            texture.LoadImage(fullDataBuffer);
            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(texture));

        }
    }
}
