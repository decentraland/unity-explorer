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
            var promise = AssetPromise<WearableDTO[], GetWearableDTOByParamIntention>.Create(World,
                GetWearableDTOByParamIntention.FromParamIntention(intention),
                partitionComponent);

            World.Add(entity, promise);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<List<Wearable>>))]
        public void FinalizeWearableDTO(in Entity entity, ref GetWearableByParamIntention originalIntention, ref AssetPromise<WearableDTO[], GetWearableDTOByParamIntention> promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<WearableDTO[]> promiseResult))
            {
                if (!promiseResult.Succeeded)
                    World.Add(entity, new StreamableLoadingResult<Wearable[]>(new Exception($"Wearable by param request failed for params {promise.LoadingIntention.Params}")));
                else
                {
                    foreach (WearableDTO wearableDto in promiseResult.Asset)
                    {
                        if (wearableCatalog.TryGetValue(wearableDto.metadata.id, out Wearable result))
                            originalIntention.Results.Add(result);
                        else
                        {
                            var wearable = new Wearable(wearableDto.metadata.id);
                            wearable.WearableDTO = new StreamableLoadingResult<WearableDTO>(wearableDto);
                            wearable.IsLoading = false;
                            originalIntention.Results.Add(wearable);
                        }
                    }

                    World.Add(entity, new StreamableLoadingResult<List<Wearable>>(originalIntention.Results));
                }
            }
        }
    }
}
