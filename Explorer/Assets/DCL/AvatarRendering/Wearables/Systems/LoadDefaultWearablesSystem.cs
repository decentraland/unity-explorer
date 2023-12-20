using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))] // It is updated first so other systems can depend on it asap
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadDefaultWearablesSystem : BaseUnityLoopSystem
    {
        private readonly WearablesDTOList defaultWearableDefinition;
        private readonly IWearableCatalog wearableCatalog;
        private readonly GameObject emptyDefaultWearable;

        internal LoadDefaultWearablesSystem(World world,
            WearablesDTOList defaultWearableDefinition, IWearableCatalog wearableCatalog, GameObject emptyDefaultWearable) : base(world)
        {
            this.defaultWearableDefinition = defaultWearableDefinition;
            this.wearableCatalog = wearableCatalog;
            this.emptyDefaultWearable = emptyDefaultWearable;
        }

        public override void Initialize()
        {
            var pointersRequest = new List<string>[BodyShape.COUNT];

            for (var i = 0; i < BodyShape.VALUES.Count; i++)
                pointersRequest[BodyShape.VALUES[i]] = new List<string>(defaultWearableDefinition.Value.Count);

            var state = new DefaultWearablesComponent(new AssetPromise<IWearable[], GetWearablesByPointersIntention>[BodyShape.COUNT]);

            for (var i = 0; i < defaultWearableDefinition.Value.Count; i++)
            {
                WearableDTO dto = defaultWearableDefinition.Value[i];
                IWearable wearable = wearableCatalog.GetOrAddWearableByDTO(dto);
                wearable.IsDefaultWearable = true;

                BodyShape analyzedBodyShape = wearable.IsCompatibleWithBodyShape(BodyShape.MALE) ? BodyShape.MALE : BodyShape.FEMALE;
                pointersRequest[analyzedBodyShape].Add(wearable.GetUrn());
            }

            for (var i = 0; i < BodyShape.VALUES.Count; i++)
            {
                BodyShape bodyShape = BodyShape.VALUES[i];
                List<string> pointers = pointersRequest[bodyShape];

                state.PromisePerBodyShape[bodyShape] = AssetPromise<IWearable[], GetWearablesByPointersIntention>
                   .Create(World, new GetWearablesByPointersIntention(pointers, new IWearable[pointers.Count], bodyShape, AssetSource.EMBEDDED, false), PartitionComponent.TOP_PRIORITY);
            }

            World.Create(state);

            // Add empty default wearable
            var wearableDTO = new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = WearablesConstants.EMPTY_DEFAULT_WEARABLE,
                },
            };

            IWearable emptyWearable = wearableCatalog.GetOrAddWearableByDTO(wearableDTO);
            emptyWearable.IsDefaultWearable = true;
            emptyWearable.IsEmptyDefaultWearableAsset = true;
            var wearableAsset = new WearableAsset(emptyDefaultWearable, new List<WearableAsset.RendererInfo>(), null);
            wearableAsset.AddReference();

            emptyWearable.WearableAssetResults[BodyShape.MALE] =
                new StreamableLoadingResult<WearableAsset>(wearableAsset);

            emptyWearable.WearableAssetResults[BodyShape.FEMALE] =
                new StreamableLoadingResult<WearableAsset>(wearableAsset);
        }

        protected override void Update(float t)
        {
            TryConsumeDefaultWearablesPromiseQuery(World);
        }

        [Query]
        private void TryConsumeDefaultWearablesPromise(ref DefaultWearablesComponent defaultWearablesComponent)
        {
            if (defaultWearablesComponent.ResolvedState != DefaultWearablesComponent.State.InProgress)
                return;

            var allPromisesAreConsumed = true;
            DefaultWearablesComponent.State finalState = DefaultWearablesComponent.State.Success;

            for (var i = 0; i < defaultWearablesComponent.PromisePerBodyShape.Length; i++)
            {
                ref AssetPromise<IWearable[], GetWearablesByPointersIntention> promise = ref defaultWearablesComponent.PromisePerBodyShape[i];
                if (promise.IsConsumed) continue;

                if (promise.TryConsume(World, out StreamableLoadingResult<IWearable[]> result))
                {
                    if (!result.Succeeded)
                        finalState = DefaultWearablesComponent.State.Fail;
                }
                else allPromisesAreConsumed = false;
            }

            if (allPromisesAreConsumed)
                defaultWearablesComponent.ResolvedState = finalState;
        }
    }
}
