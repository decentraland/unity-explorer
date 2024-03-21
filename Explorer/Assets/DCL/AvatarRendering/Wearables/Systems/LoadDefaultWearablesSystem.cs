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
using System;
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
            WearablesDTOList defaultWearableDefinition, GameObject emptyDefaultWearable,
            IWearableCatalog wearableCatalog) : base(world)
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

            var state = new DefaultWearablesComponent(new AssetPromise<WearablesResolution, GetWearablesByPointersIntention>[BodyShape.COUNT]);

            for (var i = 0; i < defaultWearableDefinition.Value.Count; i++)
            {
                WearableDTO dto = defaultWearableDefinition.Value[i];
                IWearable wearable = wearableCatalog.GetOrAddWearableByDTO(dto, false);

                BodyShape analyzedBodyShape = wearable.IsCompatibleWithBodyShape(BodyShape.MALE) ? BodyShape.MALE : BodyShape.FEMALE;
                pointersRequest[analyzedBodyShape].Add(wearable.GetUrn());
            }

            for (var i = 0; i < BodyShape.VALUES.Count; i++)
            {
                BodyShape bodyShape = BodyShape.VALUES[i];
                List<string> pointers = pointersRequest[bodyShape];

                state.PromisePerBodyShape[bodyShape] = AssetPromise<WearablesResolution, GetWearablesByPointersIntention>
                   .Create(World, new GetWearablesByPointersIntention(pointers, bodyShape, Array.Empty<string>(), AssetSource.EMBEDDED, false), PartitionComponent.TOP_PRIORITY);
            }

            // Add empty default wearable
            var wearableDTO = new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = WearablesConstants.EMPTY_DEFAULT_WEARABLE,
                },
            };

            var mesh = new Mesh();
            mesh.vertices = new []
            {
                Vector3.zero
            };
            var boneWeights = new BoneWeight[1];
            boneWeights[0].weight0 = 1; // 100% influence from the first (and only) bone
            mesh.boneWeights = boneWeights;

            var rendererInfos = new List<WearableRegularAsset.RendererInfo>();
            foreach (var skinnedMeshRenderer in emptyDefaultWearable.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinnedMeshRenderer.sharedMesh = mesh;
                rendererInfos.Add(new WearableRegularAsset.RendererInfo(skinnedMeshRenderer, skinnedMeshRenderer.sharedMaterial));
            }

            IWearable emptyWearable = wearableCatalog.GetOrAddWearableByDTO(wearableDTO, false);
            var wearableAsset = new WearableRegularAsset(emptyDefaultWearable, rendererInfos, null);
            wearableAsset.AddReference();

            // only game-objects here
            emptyWearable.AssignWearableAsset(wearableAsset, BodyShape.MALE);
            emptyWearable.AssignWearableAsset(wearableAsset, BodyShape.FEMALE);

            World.Create(state);
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
                ref AssetPromise<WearablesResolution, GetWearablesByPointersIntention> promise = ref defaultWearablesComponent.PromisePerBodyShape[i];
                if (promise.IsConsumed) continue;

                if (promise.TryConsume(World, out StreamableLoadingResult<WearablesResolution> result))
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
