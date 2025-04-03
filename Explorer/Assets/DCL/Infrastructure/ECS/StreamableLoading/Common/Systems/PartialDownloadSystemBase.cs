using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.Cacheables;
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
                // Currently Outgoing requests treat Different sources as different flows (so 2 flows can be launched for the same assets bundle intention, they are not mutually blocking)
                // it's not a problem for a non-partial flow as if EMBEDDED does not exist if will fail immediately and won't lead to the second loading flow of the same file,
                // However with partial downloading two flows end up with the same HashKey, and subsequently with the same valid file (because the file received from WEB could be already cached on DISK but now is requested from EMBEDDED) ,
                // it can lead to loading of the same file twice
                // The most proper solution would be to change OutgoingRequests to be agnostic to the CurrentSource, // TODO to be refactored later
                // Currently, the solution is to launch disk cache for WEB only as it does not make sense to read from disk cache if the file is already available in EMBEDDED assets (on disk)

                state.SetChunkData(new PartialLoadingState(await webRequestController.GetPartialAsync(intention.CommonArguments, new PartialDownloadArguments(state.PartialDownloadingData?.PartialDownloadStream), ct)));

                return await ProcessCompletedDataAsync(state, intention, partition, ct);
            }

            //This catch is a workaround for the loading breaking bug caused by multiple scenes having same asset hash
            //but with different file sizes, it won't load the asset but won't block the loading
            catch (WebRequestException e) when (e.ResponseCode == 416) { return new StreamableLoadingResult<TData>(new ReportData(), e); }
            catch (Exception)
            {
                state.SetChunkData(default(PartialLoadingState));
                throw;
            }
        }

        protected abstract UniTask<StreamableLoadingResult<TData>> ProcessCompletedDataAsync(StreamableLoadingState state, TIntention intention, IPartitionComponent partition, CancellationToken ct);
    }
}
