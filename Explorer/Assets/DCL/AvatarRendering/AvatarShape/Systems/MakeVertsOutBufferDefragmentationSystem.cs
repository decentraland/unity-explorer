using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.Diagnostics;
using ECS.Abstract;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateBefore(typeof(AvatarInstantiatorSystem))]
    public partial class MakeVertsOutBufferDefragmentationSystem : BaseUnityLoopSystem
    {
        private readonly FixedComputeBufferHandler computeBufferHandler;
        private readonly CustomSkinning skinningStrategy;

        internal MakeVertsOutBufferDefragmentationSystem(World world, FixedComputeBufferHandler computeBufferHandler, CustomSkinning skinningStrategy) : base(world)
        {
            this.computeBufferHandler = computeBufferHandler;
            this.skinningStrategy = skinningStrategy;
        }

        protected override void Update(float t)
        {
            IReadOnlyDictionary<int, FixedComputeBufferHandler.Slice> defragmentationMap = computeBufferHandler.TryMakeDefragmentation();
            if (defragmentationMap.Count == 0) return;

            // For each avatar update indices if they were moved
            UpdateIndicesQuery(World, defragmentationMap);
        }

        [Query]
        private void UpdateIndices([Data] IReadOnlyDictionary<int, FixedComputeBufferHandler.Slice> remapping, ref AvatarCustomSkinningComponent avatarCustomSkinningComponent)
        {
            if (remapping.TryGetValue(avatarCustomSkinningComponent.VertsOutRegion.StartIndex, out FixedComputeBufferHandler.Slice newRegion))
                skinningStrategy.SetVertOutRegion(newRegion, ref avatarCustomSkinningComponent);
        }
    }
}
