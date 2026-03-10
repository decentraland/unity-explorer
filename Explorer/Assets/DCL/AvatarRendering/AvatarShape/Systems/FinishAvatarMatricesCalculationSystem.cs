using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using Unity.Collections;
using Unity.Mathematics;
using System;
using RichTypes;

namespace DCL.AvatarRendering.AvatarShape
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
        private void Execute(
            ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            ref AvatarCustomSkinningComponent computeShaderSkinning
        )
        {
            Result result = computeShaderSkinning.ComputeSkinning(currentResult, avatarTransformMatrixComponent.IndexInGlobalJobArray);
            if (result.Success == false)
                ReportHub.LogException(new Exception(result.ErrorMessage), ReportCategory.AVATAR);
        }
    }
}
