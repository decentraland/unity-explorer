using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System.Collections.Generic;
using UniGLTF.SpringBoneJobs.InputPorts;
using UniVRM10.FastSpringBones;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SpringBones
{
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class SpringBoneRegistrationSystem : BaseUnityLoopSystem
    {
        private readonly FastSpringBoneService springBoneService;
        private readonly IComponentPool<Transform> transformPool;

        internal SpringBoneRegistrationSystem(World world,
            FastSpringBoneService springBoneService,
            IComponentPool<Transform> transformPool) : base(world)
        {
            this.springBoneService = springBoneService;
            this.transformPool = transformPool;
        }

        protected override void Update(float t)
        {
            RegisterNewQuery(World);
            ReRegisterOnChangeQuery(World);
            CleanupOnDeleteQuery(World);
        }

        protected override void OnDispose() =>
            CleanupOnDisposeQuery(World);

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(AvatarCustomSkinningComponent), typeof(AvatarBase), typeof(AvatarTransformMatrixComponent))]
        [None(typeof(SpringBoneRegistrationComponent), typeof(DeleteEntityIntention))]
        private void RegisterNew(in Entity entity,
            ref AvatarShapeComponent avatarShapeComponent,
            AvatarBase avatarBase,
            ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            List<Transform> boneClones = ListPool<Transform>.Get();

            FastSpringBoneBuffer buffer = BuildSpringBoneBuffer(avatarShapeComponent.InstantiatedWearables,
                avatarBase,
                ref transformMatrixComponent,
                boneClones,
                null);

            World.Add(entity,
                new SpringBoneRegistrationComponent
                {
                    Buffer = buffer,
                    AvatarVersion = avatarShapeComponent.InstantiationVersion,
                    BoneClones = boneClones,
                },
                new SpringBonePendingCloneRelease { Pending = ListPool<Transform>.Get() });
        }

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(AvatarBase), typeof(AvatarTransformMatrixComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void ReRegisterOnChange(ref AvatarShapeComponent avatarShapeComponent,
            AvatarBase avatarBase,
            ref SpringBoneRegistrationComponent registration,
            ref AvatarTransformMatrixComponent transformMatrixComponent,
            ref SpringBonePendingCloneRelease pendingRelease)
        {
            if (avatarShapeComponent.InstantiationVersion == registration.AvatarVersion) return;

            registration.AvatarVersion = avatarShapeComponent.InstantiationVersion;

            MoveToPendingRelease(registration.BoneClones, ref pendingRelease);

            registration.Buffer = BuildSpringBoneBuffer(avatarShapeComponent.InstantiatedWearables,
                avatarBase,
                ref transformMatrixComponent,
                registration.BoneClones,
                registration.Buffer);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CleanupOnDelete(in Entity entity,
            ref SpringBoneRegistrationComponent registration,
            ref SpringBonePendingCloneRelease pendingRelease)
        {
            CleanUpSpringBoneBuffer(ref registration);

            MoveToPendingRelease(registration.BoneClones, ref pendingRelease);

            ListPool<Transform>.Release(registration.BoneClones);
            registration.BoneClones = null;

            World.Remove<SpringBoneRegistrationComponent>(entity);
        }

        [Query]
        private void CleanupOnDispose(ref SpringBoneRegistrationComponent registration) =>
            CleanUpSpringBoneBuffer(ref registration);

        /// <summary>
        ///     Shared pipeline: clone spring bone chains, expand the BoneArray, build and register
        ///     the simulation buffer. If <paramref name="oldBuffer"/> is non-null it is replaced and disposed.
        /// </summary>
        private FastSpringBoneBuffer BuildSpringBoneBuffer(IList<CachedAttachment> wearables,
            AvatarBase avatarBase,
            ref AvatarTransformMatrixComponent transformMatrixComponent,
            List<Transform> clones,
            FastSpringBoneBuffer? oldBuffer)
        {
            using var _ = DictionaryPool<Transform, Transform>.Get(out var originalToClone);

            SpringBoneCloneHelper.CloneSpringBoneChains(wearables,
                avatarBase.AvatarSkinnedMeshRenderer.bones,
                transformPool,
                originalToClone,
                clones);

            if (clones.Count > 0)
            {
                transformMatrixComponent.bones = BoneArray.WithAppendedBones(transformMatrixComponent.bones, clones, GetReportCategory());
                transformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.Uninitialized();
            }

            FastSpringBoneBuffer newBuffer = SpringBoneBufferBuilder.Build(avatarBase.transform, wearables, originalToClone);

            springBoneService.BufferCombiner.Register(newBuffer, oldBuffer);
            oldBuffer?.Dispose();

            return newBuffer;
        }

        private void CleanUpSpringBoneBuffer(ref SpringBoneRegistrationComponent registration)
        {
            if (registration.Buffer == null) return;

            springBoneService.BufferCombiner.Register(null, registration.Buffer);

            registration.Buffer.Dispose();
            registration.Buffer = null;
        }

        private static void MoveToPendingRelease(List<Transform> clones, ref SpringBonePendingCloneRelease pendingRelease)
        {
            foreach (Transform clone in clones) pendingRelease.Pending.Add(clone);
            clones.Clear();
        }
    }
}
