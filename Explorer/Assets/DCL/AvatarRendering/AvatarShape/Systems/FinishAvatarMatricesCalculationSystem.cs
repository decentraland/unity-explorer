using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using ECS.Abstract;
using ECS.LifeCycle.Components;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class FinishAvatarMatricesCalculationSystem : BaseUnityLoopSystem
    {
        private readonly CustomSkinning skinningStrategy;

        internal FinishAvatarMatricesCalculationSystem(World world, CustomSkinning skinningStrategy) : base(world)
        {
            this.skinningStrategy = skinningStrategy;
        }

        protected override void Update(float t)
        {
            ExecuteQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void Execute(ref AvatarShapeComponent avatarShapeComponent, ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            ref AvatarCustomSkinningComponent computeShaderSkinning)
        {
            if (avatarShapeComponent.IsDirty)
                return;

            skinningStrategy.ComputeSkinning(avatarTransformMatrixComponent.CompleteBoneMatrixCalculations(), ref computeShaderSkinning);
        }
    }
}
