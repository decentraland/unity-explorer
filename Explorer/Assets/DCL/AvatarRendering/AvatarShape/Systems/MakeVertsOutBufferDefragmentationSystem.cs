using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class MakeVertsOutBufferDefragmentationSystem : BaseUnityLoopSystem
    {
        private readonly FixedComputeBufferHandler computeBufferHandler;

        internal MakeVertsOutBufferDefragmentationSystem(World world, FixedComputeBufferHandler computeBufferHandler) : base(world)
        {
            this.computeBufferHandler = computeBufferHandler;
        }

        protected override void Update(float t)
        {
            IReadOnlyDictionary<int, int> defragmentationMap = computeBufferHandler.TryMakeDefragmentation();
            if (defragmentationMap.Count == 0) return;

            UpdateIndicesQuery(World, defragmentationMap);
        }

        [Query]
        private void UpdateIndices([Data] IReadOnlyDictionary<int, int> remapping,
            ref AvatarComputeSkinningComponent avatarComputeSkinningComponent, ref AvatarShapeComponent avatarShapeComponent) { }
    }
}
