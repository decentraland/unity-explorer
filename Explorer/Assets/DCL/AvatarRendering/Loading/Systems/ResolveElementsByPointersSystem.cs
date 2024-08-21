using Arch.Core;
using Arch.System;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;

namespace DCL.AvatarRendering.Loading.Systems
{
    public abstract partial class ResolveElementsByPointersSystem<TIntention, TElement, TDTO, TDTOList> : BaseUnityLoopSystem
        where TIntention: IAssetIntention, IPointersLoadingIntention, IEquatable<TIntention>
        where TElement: IAvatarAttachment<TDTO> where TDTO : AvatarAttachmentDTO
    {
        private readonly IAvatarElementCache<TElement, TDTO> cache;
        private readonly ListObjectPool<URN> pointersPool;

        protected ResolveElementsByPointersSystem(
            World world,
            IAvatarElementCache<TElement, TDTO> cache,
            ListObjectPool<URN> pointersPool
        ) : base(world)
        {
            this.cache = cache;
            this.pointersPool = pointersPool;
        }

        protected bool TryFinalizeIfCancelled(Entity entity, in AssetPromise<TDTOList, TIntention> promise)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested == false)
                return false;

            foreach (var pointerID in promise.LoadingIntention.Pointers)
                if (cache.TryGetElement(pointerID, out var component))
                    component.IsLoading = false;

            promise.ForgetLoading(World!);
            World!.Destroy(entity);
            return true;
        }

        protected void ReportAndFinalizeWithError(URN urn)
        {
            //We have some missing pointers that were not completed. We have to consider them as failure
            var e = new ArgumentNullException($"Wearable DTO is null for for {urn}");
            ReportHub.LogError(new ReportData(GetReportCategory()), e);

            if (cache.TryGetElement(urn, out var component))
            {
                //If its not in the catalog, we cannot determine which one has failed
                component.ResolvedFailedDTO(new StreamableLoadingResult<TDTO>(e));
                component.IsLoading = false;
            }
        }

        /*[Query]
        private void FinalizeWearableDTO(in Entity entity, ref AssetPromise<TDTOList, TIntention> promise, ref BodyShape bodyShape) //use bodyshape? TODO
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                foreach (var pointerID in promise.LoadingIntention.Pointers)
                {
                    cache.TryGetElement(pointerID, out var component);
                    component.IsLoading = false;
                }

                promise.ForgetLoading(World!);
                World!.Destroy(entity);
                return;
            }

            if (promise.SafeTryConsume(World!, out StreamableLoadingResult<TDTOList> promiseResult))
            {
                if (!promiseResult.Succeeded)

                    //No wearable representation is going to be possible
                    foreach (string pointerID in promise.LoadingIntention.Pointers)
                        ReportAndFinalizeWithError(pointerID);
                else
                {
                    using var _ = pointersPool.Get(out var failedDTOList);
                    failedDTOList!.AddRange(promise.LoadingIntention.Pointers);

                    //promiseResult.Asset.Value todo
                    foreach (var assetEntity in promiseResult.Asset.Value)
                    {
                        bool isWearableInCatalog = cache.TryGetElement(assetEntity.metadata.id, out var component);

                        if (!isWearableInCatalog)
                        {
                            //A wearable that has a DTO request should already have an empty representation in the catalog at this point
                            ReportHub.LogError(new ReportData(GetReportCategory()), $"Requested element {typeof(TDTO).Name} {assetEntity.metadata.id} is not in the catalog");
                            continue;
                        }

                        if (!component.TryResolveDTO(new StreamableLoadingResult<TDTO>(assetEntity)))
                            ReportHub.LogError(new ReportData(GetReportCategory()), $"Element {typeof(TDTO).Name} DTO has already been initialized: {assetEntity.metadata.id}");

                        failedDTOList.Remove(assetEntity.metadata.id);
                        component.IsLoading = false;
                    }

                    //If this list is not empty, it means we have at least one unresolvedDTO that was not completed. We need to finalize it as error
                    foreach (var urn in failedDTOList)
                        ReportAndFinalizeWithError(urn);
                }

                promise.LoadingIntention.ReleasePointers();
                World.Destroy(entity);

                void ReportAndFinalizeWithError(URN urn)
                {
                    //We have some missing pointers that were not completed. We have to consider them as failure
                    var e = new ArgumentNullException($"Wearable DTO is null for for {urn}");
                    ReportHub.LogError(new ReportData(GetReportCategory()), e);

                    if (cache.TryGetElement(urn, out var component))
                    {
                        //If its not in the catalog, we cannot determine which one has failed
                        component.ResolvedFailedDTO(new StreamableLoadingResult<TDTO>(e));
                        component.IsLoading = false;
                    }
                }
            }
        }*/
    }
}
