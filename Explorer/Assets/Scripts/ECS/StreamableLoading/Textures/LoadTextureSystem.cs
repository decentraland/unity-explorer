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
        //private readonly ArrayPool<byte> arrayPool = ArrayPool<byte>.Create();
        private readonly IWebRequestController webRequestController;

        //Create proper structure to handle the chunk count, the structure can hold the chunk count, progress, full file size, time to live, etc
        private readonly Dictionary<GetTextureIntention, int> currentRequests = new Dictionary<GetTextureIntention, int>();

        //Another system might be fed with the dictionary to handle the chunk expiry and other additional logic

        internal LoadTextureSystem(World world, IStreamableCache<Texture2DData, GetTextureIntention> cache, IWebRequestController webRequestController) : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetTextureIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct, EntityReference entity)
        {
            //TODO: use proper pool
            byte[] partialDownloadBuffer = new byte[CHUNK_SIZE];//arrayPool.Rent(CHUNK_SIZE);

            //Create the new partial downloading data
            PartialDownloadingData partialDownloadingData = new PartialDownloadingData(partialDownloadBuffer, 0, CHUNK_SIZE);

            /*TODO add the proper flow to do the first request, handle the partial result if supports partial download and
             handle direct result creation if partial is not supported or file is smaller than chunk*/

            await webRequestController.GetPartialAsync(
                intention.CommonArguments,
                ct,
                reportData: ReportCategory.PARTIAL_LOADING,
                partialDownloadingData,
                headersInfo: new WebRequestHeadersInfo().WithRange(partialDownloadingData.RangeStart, partialDownloadingData.RangeEnd));

            //Add proper handling of the chunk count
            currentRequests.Add(intention, 1);

            var arguments = intention.CommonArguments;
            arguments.HasChunkDownloadStarted = true;
            UpdateState(entity, arguments);

            //Allocate the full data buffer based on full file size
            //Temporary solution atm, will change to a Memory or Stream
            byte[] fullDataBuffer = new byte[partialDownloadingData.FullFileSize];//arrayPool.Rent(partialDownloadingData.FullFileSize);

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
                StreamableLoadingState state = ReleaseBudget(entity);
                await UniTask.WaitUntil(() => state.Value == StreamableLoadingState.Status.Allowed);
            }

            //arrayPool.Return(partialDownloadBuffer, true);

            //Verify if this is the proper flow
            var texture = new Texture2D(1, 1);
            texture.LoadImage(fullDataBuffer);
            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(texture));

        }

        //The following 2 functions are workarounds to allow refs in an async context, would be interesting to see if there is a better way to handle this
        private void UpdateState(EntityReference entity, CommonLoadingArguments arguments)
        {
            ref GetTextureIntention getIntention = ref World.TryGetRef<GetTextureIntention>(entity, out _);
            getIntention.CommonArguments = arguments;
        }

        private StreamableLoadingState ReleaseBudget(EntityReference entity)
        {
            ref StreamableLoadingState state = ref World!.TryGetRef<StreamableLoadingState>(entity, out _);
            state.AcquiredBudget.Release();
            state.SetChunkCompleted();
            return state;
        }
    }
}
