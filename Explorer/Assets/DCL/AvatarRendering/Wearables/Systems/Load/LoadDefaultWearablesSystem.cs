using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS.Abstract;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Systems.Load
{
    [UpdateInGroup(typeof(InitializationSystemGroup))] // It is updated first so other systems can depend on it asap
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadDefaultWearablesSystem : BaseUnityLoopSystem
    {
        private readonly IWearableStorage wearableStorage;
        private readonly GameObject emptyDefaultWearable;

        internal LoadDefaultWearablesSystem(World world,
            GameObject emptyDefaultWearable,
            IWearableStorage wearableStorage) : base(world)
        {
            this.wearableStorage = wearableStorage;
            this.emptyDefaultWearable = emptyDefaultWearable;
        }

        public override void Initialize()
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

        protected override void Update(float t)
        {
        }
    }
}
