using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    public partial class FinishAvatarMatricesCalculationSystem : BaseUnityLoopSystem
    {
        private readonly CustomSkinning skinningStrategy;
        private readonly AvatarTransformMatrixJobWrapper jobWrapper;
        private NativeArray<float4x4> currentResult;

        internal FinishAvatarMatricesCalculationSystem(World world, CustomSkinning skinningStrategy,
            AvatarTransformMatrixJobWrapper jobWrapper) : base(world)
        {
            this.skinningStrategy = skinningStrategy;
            this.jobWrapper = jobWrapper;
        }

        protected override void Update(float t)
        {
            jobWrapper.CompleteBoneMatrixCalculations();
            currentResult = jobWrapper.job.BonesMatricesResult;
            ExecuteQuery(World);
        }

        [Query]
        [All(typeof(AvatarShapeComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void Execute(ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            ref AvatarCustomSkinningComponent computeShaderSkinning)
        {
            skinningStrategy.ComputeSkinning(currentResult, avatarTransformMatrixComponent.IndexInGlobalJobArray, ref computeShaderSkinning);
        }
    }
}
