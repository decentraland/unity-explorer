using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Common.Systems
{
    public abstract class PartialDownloadSystemBase<TData, TIntention> : LoadSystemBase<TData, TIntention>
        where TIntention: struct, ILoadingIntention
    {
        private readonly IWebRequestController webRequestController;

        protected PartialDownloadSystemBase(
            World world,
            IStreamableCache<TData, TIntention> cache,
            IWebRequestController webRequestController)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<TData>> FlowInternalAsync(TIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            try
            {
                PartialDownloadStream? partialDownloadStream = await webRequestController.GetPartialAsync(intention.CommonArguments, GetReportData(), new PartialDownloadArguments(state.PartialDownloadingData?.PartialDownloadStream))
                                                                                         .GetStreamAsync(ct);
                state.SetChunkData(new PartialLoadingState(partialDownloadStream));

                ct.ThrowIfCancellationRequested();

                if (partialDownloadStream.IsFullyDownloaded)
                    return await ProcessCompletedDataAsync(state, intention, partition, ct);

                return default(StreamableLoadingResult<TData>);
            }

            //This catch is a workaround for the loading breaking bug caused by multiple scenes having same asset hash
            //but with different file sizes, it won't load the asset but won't block the loading
            catch (WebRequestException e) when (e.ResponseCode == 416) { return new StreamableLoadingResult<TData>(new ReportData(), e); }
            catch (Exception)
            {
                state.PartialDownloadingData = null;
                throw;
            }
        }

        protected abstract UniTask<StreamableLoadingResult<TData>> ProcessCompletedDataAsync(StreamableLoadingState state, TIntention intention, IPartitionComponent partition, CancellationToken ct);
    }
}
