using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ResolveWearableByParamIntentionSystem : BaseUnityLoopSystem
    {
        private readonly Dictionary<string, Wearable> wearableCatalog;

        public ResolveWearableByParamIntentionSystem(World world, Dictionary<string, Wearable> wearableCatalog) : base(world)
        {
            this.wearableCatalog = wearableCatalog;
        }

        protected override void Update(float t)
        {
            ResolveWearablePromiseQuery(World);
            FinalizeWearableDTOQuery(World);
        }

        //TODO: Why cant I use IPartitionComponent here?
        [Query]
        [None(typeof(AssetPromise<WearableDTO[], GetWearableDTOByParamIntention>))]
        public void ResolveWearablePromise(in Entity entity, ref GetWearableByParamIntention intention, ref PartitionComponent partitionComponent)
        {
            var promise = AssetPromise<WearableDTO[], GetWearableDTOByParamIntention>.Create(World,
                new GetWearableDTOByParamIntention
                {
                    CommonArguments = new CommonLoadingArguments("DummyUser"),
                    Params = intention.Params,
                    UserID = intention.UserID,
                }, partitionComponent);

            World.Add(entity, promise);
        }

        [Query]
        [None(typeof(Wearable[]))]
        public void FinalizeWearableDTO(in Entity entity, ref AssetPromise<WearableDTO[], GetWearableDTOByParamIntention> promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<WearableDTO[]> promiseResult))
            {
                if (!promiseResult.Succeeded)
                    ReportHub.Log(GetReportCategory(), $"Wearable by param request failed for params {promise.LoadingIntention.Params}");
                else
                {
                    var newWearables = new List<Wearable>();

                    foreach (WearableDTO assetEntity in promiseResult.Asset)
                    {
                        //TODO: POOL!!!!
                        //TODO: Download Thumbnail
                        if (!wearableCatalog.ContainsKey(assetEntity.metadata.id))
                        {
                            var wearable = new Wearable(assetEntity.metadata.id);
                            wearable.WearableDTO = new StreamableLoadingResult<WearableDTO>(assetEntity);
                            wearable.IsLoading = false;
                            newWearables.Add(wearable);
                        }
                    }

                    World.Add(entity, newWearables.ToArray());
                }
            }
        }
    }
}
