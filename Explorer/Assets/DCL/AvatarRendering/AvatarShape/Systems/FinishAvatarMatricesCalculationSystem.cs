using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(StartAvatarMatricesCalculationSystem))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class FinishAvatarMatricesCalculationSystem : BaseUnityLoopSystem
    {
        private readonly CustomSkinning skinningStrategy;
        private readonly AvatarTransformMatrixJobWrapper jobWrapper;

        internal FinishAvatarMatricesCalculationSystem(World world, CustomSkinning skinningStrategy, ref AvatarTransformMatrixJobWrapper jobWrapper) : base(world)
        {
            this.skinningStrategy = skinningStrategy;
            this.jobWrapper = jobWrapper;
        }

        protected override void Update(float t)
        {
            jobWrapper.CompleteBoneMatrixCalculations();
            ExecuteQuery(World);
        }

        [Query]
        [All(typeof(AvatarShapeComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void Execute(ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            ref AvatarCustomSkinningComponent computeShaderSkinning)
        {
            skinningStrategy.ComputeSkinning(jobWrapper.GetResultForIndex(avatarTransformMatrixComponent.IndexInGlobalJobArray), ref computeShaderSkinning);
        }
    }
}
