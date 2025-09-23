using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;

namespace DCL.AvatarRendering.Loading.Systems.Abstract
{
    public abstract class FinalizeElementsLoadingSystem<TIntention, TElement, TDTO, TDTOList> : BaseUnityLoopSystem
        where TIntention: IAssetIntention, IPointersLoadingIntention, IEquatable<TIntention>
        where TElement: IAvatarAttachment<TDTO> where TDTO: AvatarAttachmentDTO
    {
        protected readonly IAvatarElementStorage<TElement, TDTO> storage;
        private readonly ListObjectPool<URN> pointersPool;

        protected FinalizeElementsLoadingSystem(
            World world,
            IAvatarElementStorage<TElement, TDTO> storage,
            ListObjectPool<URN> pointersPool
        ) : base(world)
        {
            this.storage = storage;
            this.pointersPool = pointersPool;
        }

        protected bool TryFinalizeIfCancelled(Entity entity, in AssetPromise<TDTOList, TIntention> promise)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested == false)
                return false;

            foreach (URN pointerID in promise.LoadingIntention.Pointers)
                if (storage.TryGetElement(pointerID, out TElement component))
                    component.UpdateLoadingStatus(false);

            promise.ForgetLoading(World!);
            World!.Destroy(entity);
            return true;
        }

        protected void ReportAndFinalizeWithError(URN urn)
        {
            //We have some missing pointers that were not completed. We have to consider them as failure

            if (storage.TryGetElement(urn, out TElement component))
            {
                //If its not in the catalog, we cannot determine which one has failed
                component.ResolvedFailedDTO(
                    new StreamableLoadingResult<TDTO>(
                        new ReportData(GetReportCategory()),
                        new ArgumentNullException($"Wearable DTO is null for for {urn}")
                    )
                );

                component.UpdateLoadingStatus(false);
            }
        }
    }
}
