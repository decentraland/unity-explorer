﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Systems.Load
{
    [UpdateInGroup(typeof(InitializationSystemGroup))] // It is updated first so other systems can depend on it asap
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadDefaultWearablesSystem : BaseUnityLoopSystem
    {
        private readonly WearablesDTOList defaultWearableDefinition;
        private readonly IWearableStorage wearableStorage;
        private readonly IRealmData realm;
        private readonly GameObject emptyDefaultWearable;

        internal LoadDefaultWearablesSystem(World world,
            WearablesDTOList defaultWearableDefinition, GameObject emptyDefaultWearable,
            IWearableStorage wearableStorage,
            IRealmData realm) : base(world)
        {
            this.defaultWearableDefinition = defaultWearableDefinition;
            this.wearableStorage = wearableStorage;
            this.realm = realm;
            this.emptyDefaultWearable = emptyDefaultWearable;
        }

        public override void Initialize()
        {
            AddEmptyWearable();

            World.Create(new DefaultWearablesComponent(new AssetPromise<WearablesResolution, GetWearablesByPointersIntention>[BodyShape.COUNT]));
        }

        protected override void Update(float t)
        {
            TryInitializeDefaultWearablesQuery(World);
            TryConsumeDefaultWearablesPromiseQuery(World);
        }

        [Query]
        private void TryInitializeDefaultWearables(ref DefaultWearablesComponent state)
        {
            // We need to wait until the realm is configured so we can generate GetWearablesByPointersIntention for the assets
            if (!realm.Configured) return;
            if (state.ResolvedState != DefaultWearablesComponent.State.None) return;

            state.ResolvedState = DefaultWearablesComponent.State.InProgress;

            var pointersRequest = new List<URN>[BodyShape.COUNT];
            using var consumedDefaultWearableDefinition = defaultWearableDefinition.ConsumeAttachments();

            for (var i = 0; i < BodyShape.VALUES.Count; i++)
                pointersRequest[BodyShape.VALUES[i]] = new List<URN>(consumedDefaultWearableDefinition.Value.Count);

            for (var i = 0; i < consumedDefaultWearableDefinition.Value.Count; i++)
            {
                WearableDTO dto = consumedDefaultWearableDefinition.Value[i];
                IWearable wearable = wearableStorage.AddDefaultWearableByDTO(dto);
                BodyShape analyzedBodyShape = wearable.IsCompatibleWithBodyShape(BodyShape.MALE) ? BodyShape.MALE : BodyShape.FEMALE;
                pointersRequest[analyzedBodyShape].Add(wearable.GetUrn());
            }

            for (var i = 0; i < BodyShape.VALUES.Count; i++)
            {
                BodyShape bodyShape = BodyShape.VALUES[i];
                List<URN> pointers = pointersRequest[bodyShape];

                state.PromisePerBodyShape[bodyShape] = AssetPromise<WearablesResolution, GetWearablesByPointersIntention>
                   .Create(World, new GetWearablesByPointersIntention(pointers, bodyShape, Array.Empty<string>(), AssetSource.EMBEDDED, false), PartitionComponent.TOP_PRIORITY);
            }
        }

        [Query]
        private void TryConsumeDefaultWearablesPromise(ref DefaultWearablesComponent defaultWearablesComponent)
        {
            if (defaultWearablesComponent.ResolvedState != DefaultWearablesComponent.State.InProgress)
                return;

            if (defaultWearablesComponent.ResolvedState == DefaultWearablesComponent.State.None)
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

        private void AddEmptyWearable()
        {
            // Add empty default wearable
            var wearableDTO = new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = WearablesConstants.EMPTY_DEFAULT_WEARABLE,
                    data = new WearableDTO.WearableMetadataDto.DataDto
                    {
                        category = WearablesConstants.Categories.HELMET
                    }
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

            var rendererInfos = new List<AttachmentRegularAsset.RendererInfo>();
            foreach (var skinnedMeshRenderer in emptyDefaultWearable.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinnedMeshRenderer.sharedMesh = mesh;
                rendererInfos.Add(new AttachmentRegularAsset.RendererInfo(skinnedMeshRenderer.sharedMaterial));
            }

            IWearable emptyWearable = wearableStorage.GetOrAddByDTO(wearableDTO, false);
            var wearableAsset = new AttachmentRegularAsset(emptyDefaultWearable, rendererInfos, null);
            wearableAsset.AddReference();

            // only game-objects here
            emptyWearable.AssignWearableAsset(wearableAsset, BodyShape.MALE);
            emptyWearable.AssignWearableAsset(wearableAsset, BodyShape.FEMALE);
        }
    }
}
