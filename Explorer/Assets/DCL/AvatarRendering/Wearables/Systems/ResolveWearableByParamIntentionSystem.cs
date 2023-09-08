using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine;

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

        [Query]
        [None(typeof(AssetPromise<WearableDTO[], GetWearableDTOByParamIntention>))]
        public void ResolveWearablePromise(in Entity entity, ref GetWearableByParamIntention intention, ref IPartitionComponent partitionComponent)
        {
            Debug.Log("BBBBBB");
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
        [None(typeof(StreamableLoadingResult<Wearable[]>))]
        public void FinalizeWearableDTO(in Entity entity, ref AssetPromise<WearableDTO[], GetWearableDTOByParamIntention> promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<WearableDTO[]> promiseResult))
            {
                if (!promiseResult.Succeeded)
                    World.Add(entity, new StreamableLoadingResult<Wearable[]>(new Exception($"Wearable by param request failed for params {promise.LoadingIntention.Params}")));
                else
                {
                    //TODO: POOL!!!!
                    var newWearables = new List<Wearable>();
                    foreach (WearableDTO assetEntity in promiseResult.Asset)
                    {
                        //TODO: Download Thumbnail
                        if (!wearableCatalog.ContainsKey(assetEntity.metadata.id))
                        {
                            var wearable = new Wearable(assetEntity.metadata.id);
                            wearable.WearableDTO = new StreamableLoadingResult<WearableDTO>(assetEntity);
                            wearable.IsLoading = false;
                            newWearables.Add(wearable);
                        }
                    }

                    World.Add(entity, new StreamableLoadingResult<Wearable[]>(newWearables.ToArray()));
                }
            }
        }
    }
}
