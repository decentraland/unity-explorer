using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using Unity.Collections;
using Unity.Mathematics;
using System;
using DCL.CharacterCamera;
using RichTypes;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    public partial class FinishAvatarMatricesCalculationSystem : BaseUnityLoopSystem
    {
        private readonly AvatarTransformMatrixJobWrapper jobWrapper;

        // Reused frustum-plane scratch buffer, rewritten once per tick before the query reads it.
        private readonly Plane[] frustumPlanes = new Plane[6];

        private NativeArray<float4x4> remoteResult;
        private NativeArray<float4x4> mainPlayerResult;
        private NativeArray<float3x2> worldBounds;

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
            worldBounds = jobWrapper.RemoteAvatarsWorldBounds;

            // This group runs in PostLateUpdate, after every camera system, so the planes are final for the
            // frame - and the bounds they are tested against were placed in the world by the calculation job.
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
            // The main player never skips: reflections and portraits sample it outside this frustum. Preview
            // avatars are drawn by their own camera into a render texture, so the player camera says nothing
            // about them either.
            bool culled = !avatarTransformMatrixComponent.IsMainPlayer
                          && !avatarShape.IsPreview
                          && (!avatarShape.IsVisible || !IsInFrustum(avatarTransformMatrixComponent.IndexInGlobalJobArray));

            // Unity's own animator culling only consults SkinnedMeshRenderers and the custom skinning
            // pipeline deletes them all, so visibility must gate the Animator manually
            if (avatarBase.AvatarAnimator.enabled == culled)
                avatarBase.AvatarAnimator.enabled = !culled;

            if (!computeShaderSkinning.ForceSkinNextFrame && culled)
                return;

            computeShaderSkinning.ForceSkinNextFrame = false;

            NativeArray<float4x4> bonesResult = avatarTransformMatrixComponent.IsMainPlayer
                ? mainPlayerResult
                : remoteResult;

            Result result = computeShaderSkinning.ComputeSkinning(bonesResult, avatarTransformMatrixComponent.IndexInGlobalJobArray);

            if (result.Success == false)
                ReportHub.LogException(new Exception(result.ErrorMessage), ReportCategory.AVATAR);
        }

        private bool IsInFrustum(GlobalJobArrayIndex indexInGlobalJobArray)
        {
            // An avatar that has not been registered into the job yet has no bounds to test, so it is kept
            // alive rather than culled on missing data
            if (indexInGlobalJobArray.TryGetValue(out int validIndex) == false || validIndex >= worldBounds.Length)
                return true;

            float3x2 bounds = worldBounds[validIndex];
            return GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(bounds.c0, bounds.c1 * 2f));
        }
    }
}
