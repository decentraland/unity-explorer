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
using UnityEngine;
using System;
using DCL.CharacterCamera;
using RichTypes;
using Utility;

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

        // Golden-capture diagnostic: hash every avatar's bone-matrix slot once after the
        // freeze so two boots can be compared at the exact skinning input, without any
        // GPU capture. One line per avatar keyed by its shape name.
        private static readonly bool GOLDEN_BONE_HASH = Environment.GetEnvironmentVariable("DCL_PLAZABENCH_DETERM_CLOCK") == "1";
        private static readonly double BONE_HASH_AT = (double.TryParse(Environment.GetEnvironmentVariable("DCL_GOLDEN_FREEZE_AT"), out double fz) ? fz : 480.0) + 5.0;
        private int boneHashDumps;
        private double frozenSince;
        private System.Text.StringBuilder boneHashSb;

        // Golden capture: read once per tick, before the query, so every avatar sees the same value.
        private bool goldenFrozen;

        protected override void Update(float t)
        {
            jobWrapper.CompleteBoneMatrixCalculations();
            remoteResult = jobWrapper.RemoteAvatarsBonesResult;
            mainPlayerResult = jobWrapper.MainPlayerBonesResult;
            worldBounds = jobWrapper.RemoteAvatarsWorldBounds;

            // Extracted here rather than at schedule time so the planes and the completed job output are read
            // in the same place, one tick of the player loop apart at most.
            GeometryUtility.CalculateFrustumPlanes(camera.GetCameraComponent(World).Camera, frustumPlanes);

            goldenFrozen = (AppDomain.CurrentDomain.GetData("golden.frozen") as bool?) == true;

            ExecuteQuery(World);

            double now = (DateTime.UtcNow - ProcessEpoch.StartUtc).TotalSeconds;

            if (goldenFrozen && frozenSince == 0.0) frozenSince = now;

            if (GOLDEN_BONE_HASH && goldenFrozen && boneHashDumps < 2 && now > frozenSince + 2.0 + boneHashDumps * 10.0)
            {
                boneHashDumps++;
                boneHashSb = new System.Text.StringBuilder(64 * 1024);
                boneHashSb.AppendLine($"section {boneHashDumps} t={now:F1} time={UnityEngine.Time.timeAsDouble:R} unscaled={UnityEngine.Time.unscaledTimeAsDouble:R} dt={UnityEngine.Time.deltaTime:R} udt={UnityEngine.Time.unscaledDeltaTime:R}");

                var blenders = UnityEngine.Object.FindObjectsByType<DCL.AvatarRendering.AvatarShape.UnityInterface.MaskedLegacyEmoteBlender>(FindObjectsSortMode.None);
                boneHashSb.Append($"blenders n={blenders.Length} en=[");
                System.Reflection.FieldInfo tf = typeof(DCL.AvatarRendering.AvatarShape.UnityInterface.MaskedLegacyEmoteBlender).GetField("time", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                for (var bi = 0; bi < Math.Min(blenders.Length, 5); bi++)
                    boneHashSb.Append($"{blenders[bi].enabled}:{(tf != null ? tf.GetValue(blenders[bi]) : "?")},");

                boneHashSb.AppendLine("]");
                DumpBoneHashQuery(World);

                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"golden-bonehash-{System.Diagnostics.Process.GetCurrentProcess().Id}.txt"),
                        boneHashSb.ToString());
                }
                catch (Exception) { /* diagnostics only */ }

                boneHashSb = null;
            }
        }

        [Query]
        [All(typeof(AvatarShapeComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void DumpBoneHash(
            in AvatarShapeComponent shape,
            ref AvatarTransformMatrixComponent matrixComponent,
            ref AvatarCustomSkinningComponent skinning)
        {
            if (!matrixComponent.IndexInGlobalJobArray.TryGetValue(out int idx)) return;

            NativeArray<float4x4> arr = matrixComponent.IsMainPlayer ? mainPlayerResult : remoteResult;
            int start = idx * ComputeShaderConstants.MAX_BONE_COUNT;
            var h = 2166136261u;

            for (var i = 0; i < skinning.BoneCount && start + i < arr.Length; i++)
            {
                float4x4 m = arr[start + i];

                for (var c = 0; c < 4; c++)
                for (var r = 0; r < 4; r++)
                    unchecked { h = (h ^ math.asuint(m[c][r])) * 16777619u; }
            }

            boneHashSb.Append($"bonehash {shape.Name} idx={idx} bones={skinning.BoneCount} h={h:X8} m0=");

            float4x4 m0 = arr[start];

            for (var c = 0; c < 4; c++)
            for (var r = 0; r < 4; r++)
                boneHashSb.Append($"{m0[c][r]:R},");

            if (!matrixComponent.IsMainPlayer)
                jobWrapper.GoldenDescribeSlot(idx, boneHashSb);

            boneHashSb.Append(" pb=");

            for (var i = 0; i < skinning.BoneCount && start + i < arr.Length; i++)
            {
                float4x4 m = arr[start + i];
                var bh = 2166136261u;

                for (var c = 0; c < 4; c++)
                for (var r = 0; r < 4; r++)
                    unchecked { bh = (bh ^ math.asuint(m[c][r])) * 16777619u; }

                boneHashSb.Append($"{bh:X8},");
            }

            boneHashSb.AppendLine();
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
            bool exempt = AvatarCullingRule.IsExempt(avatarTransformMatrixComponent.IsMainPlayer, avatarShape.IsPreview);

            // The || short-circuits, so an exempt avatar never indexes the remote bounds array.
            bool culled = AvatarCullingRule.IsCulled(exempt, avatarShape.IsVisible,
                exempt || IsInFrustum(avatarTransformMatrixComponent.IndexInGlobalJobArray));

            // Unity's own animator culling only consults SkinnedMeshRenderers and the custom skinning
            // pipeline deletes them all, so visibility must gate the Animator manually. The rule is a strict
            // refinement of the one AvatarShapeVisibilitySystem applies on its transitions, so the two writers
            // agree whenever nothing changed: no footstep FX inside a hide-avatars modifier area, which culling
            // covers for everyone but the exempt avatars, and never while a legacy Animation drives the rig.
            bool animatorShouldRun = !culled && !avatarShape.HiddenByModifierArea && !avatarBase.IsLegacyAnimationPlaying;

            // Golden capture: once the harness signals frozen, animator enablement stays wherever the freeze put
            // it (same rule as AvatarShapeVisibilitySystem); culling then only skips the skinning below.
            if (!goldenFrozen && avatarBase.AvatarAnimator.enabled != animatorShouldRun)
                avatarBase.AvatarAnimator.enabled = animatorShouldRun;

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
            // alive rather than culled on missing data.
            if (indexInGlobalJobArray.TryGetValue(out int validIndex) == false || validIndex >= worldBounds.Length)
                return true;

            float3x2 bounds = worldBounds[validIndex];
            return GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(bounds.c0, bounds.c1 * 2f));
        }
    }
}
