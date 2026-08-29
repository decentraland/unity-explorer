using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using Unity.Collections;
using Unity.Mathematics;
using System;
using RichTypes;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    public partial class FinishAvatarMatricesCalculationSystem : BaseUnityLoopSystem
    {
        private readonly AvatarTransformMatrixJobWrapper jobWrapper;
        private readonly Plane[] frustumPlanes = new Plane[6];
        private NativeArray<float4x4> remoteResult;
        private NativeArray<float4x4> mainPlayerResult;
        private SingleInstanceEntity camera;

        internal FinishAvatarMatricesCalculationSystem(World world, AvatarTransformMatrixJobWrapper jobWrapper) : base(world)
        {
            this.jobWrapper = jobWrapper;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            jobWrapper.CompleteBoneMatrixCalculations();
            remoteResult = jobWrapper.RemoteAvatarsBonesResult;
            mainPlayerResult = jobWrapper.MainPlayerBonesResult;

            GeometryUtility.CalculateFrustumPlanes(camera.GetCameraComponent(World).Camera, frustumPlanes);

            ExecuteQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void Execute(
            ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            ref AvatarCustomSkinningComponent computeShaderSkinning,
            in AvatarShapeComponent avatarShape,
            in AvatarBase avatarBase
        )
        {
            // Skinned verts persist between dispatches, so hidden and out-of-frustum avatars can skip theirs;
            // the main player never skips (reflections and portraits sample it outside this frustum test)
            if (computeShaderSkinning.ForceSkinNextFrame)
                computeShaderSkinning.ForceSkinNextFrame = false;
            else if (!avatarTransformMatrixComponent.IsMainPlayer
                     && (!avatarShape.IsVisible || !GeometryUtility.TestPlanesAABB(frustumPlanes, avatarBase.AvatarSkinnedMeshRenderer.bounds)))
                return;

            NativeArray<float4x4> bonesResult = avatarTransformMatrixComponent.IsMainPlayer
                ? mainPlayerResult
                : remoteResult;

            Result result = computeShaderSkinning.ComputeSkinning(bonesResult, avatarTransformMatrixComponent.IndexInGlobalJobArray);

            if (result.Success == false)
                ReportHub.LogException(new Exception(result.ErrorMessage), ReportCategory.AVATAR);
        }
    }
}
