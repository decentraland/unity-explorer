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
        private readonly AvatarTransformMatrixJobWrapper jobWrapper;
        private NativeArray<float4x4> remoteResult;
        private NativeArray<float4x4> mainPlayerResult;

        internal FinishAvatarMatricesCalculationSystem(World world, AvatarTransformMatrixJobWrapper jobWrapper) : base(world)
        {
            this.jobWrapper = jobWrapper;
        }

        protected override void Update(float t)
        {
            jobWrapper.CompleteBoneMatrixCalculations();
            remoteResult = jobWrapper.RemoteAvatarsBonesResult;
            mainPlayerResult = jobWrapper.MainPlayerBonesResult;

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
            NativeArray<float4x4> bonesResult = avatarTransformMatrixComponent.IsMainPlayer
                ? mainPlayerResult
                : remoteResult;

            Result result = computeShaderSkinning.ComputeSkinning(bonesResult, avatarTransformMatrixComponent.IndexInGlobalJobArray);

            if (result.Success == false)
                ReportHub.LogException(new Exception(result.ErrorMessage), ReportCategory.AVATAR);
        }
    }
}
