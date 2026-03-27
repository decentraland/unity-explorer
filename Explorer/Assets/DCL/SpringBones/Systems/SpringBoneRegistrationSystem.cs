using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
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
    /// <summary>
    ///     Manages spring bone buffer registration with the FastSpringBone simulation service.
    ///     Creates clone transforms under the avatar skeleton, builds simulation buffers,
    ///     and patches the BoneArray for the skinning compute shader.
    /// </summary>
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class SpringBoneRegistrationSystem : BaseUnityLoopSystem
    {
        private readonly FastSpringBoneService springBoneService;
        private readonly IComponentPool<Transform> transformPool;

        internal SpringBoneRegistrationSystem(World world, FastSpringBoneService springBoneService,
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

        protected override void OnDispose()
        {
            CleanupOnDisposeQuery(World);
        }

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(AvatarCustomSkinningComponent), typeof(AvatarBase), typeof(AvatarTransformMatrixComponent))]
        [None(typeof(SpringBoneRegistrationComponent), typeof(DeleteEntityIntention))]
        private void RegisterNew(in Entity entity, ref AvatarShapeComponent avatarShapeComponent,
            AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            using var scope = DictionaryPool<Transform, Transform>.Get(out var originalToClone);

            Transform[] clones = SpringBoneCloneHelper.CloneSpringBoneChains(
                avatarShapeComponent.InstantiatedWearables, avatarBase.AvatarSkinnedMeshRenderer.bones, transformPool, originalToClone);

            PatchBoneArray(ref transformMatrixComponent, originalToClone);

            FastSpringBoneBuffer buffer = SpringBoneBufferBuilder.Build(avatarBase.transform, avatarShapeComponent.InstantiatedWearables, originalToClone);

            if (buffer != null) springBoneService.BufferCombiner.Register(buffer, null);

            World.Add(entity, new SpringBoneRegistrationComponent
            {
                Buffer = buffer,
                LastKnownVersion = avatarShapeComponent.InstantiationVersion,
                Clones = clones,
            },
            new SpringBonePendingCloneRelease { Clones = new List<Transform>() });
        }

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(AvatarBase), typeof(AvatarTransformMatrixComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void ReRegisterOnChange(ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase,
            ref SpringBoneRegistrationComponent registration, ref AvatarTransformMatrixComponent transformMatrixComponent,
            ref SpringBonePendingCloneRelease pendingRelease)
        {
            if (avatarShapeComponent.InstantiationVersion == registration.LastKnownVersion) return;

            registration.LastKnownVersion = avatarShapeComponent.InstantiationVersion;

            // Defer release of old clones — they're still in the combined TAA until ManualUpdate flushes
            AddClonesToPendingRelease(registration.Clones, ref pendingRelease);

            FastSpringBoneBuffer oldBuffer = registration.Buffer;

            using var scope = DictionaryPool<Transform, Transform>.Get(out var originalToClone);

            Transform[] clones = SpringBoneCloneHelper.CloneSpringBoneChains(
                avatarShapeComponent.InstantiatedWearables, avatarBase.AvatarSkinnedMeshRenderer.bones, transformPool, originalToClone);

            PatchBoneArray(ref transformMatrixComponent, originalToClone);

            FastSpringBoneBuffer newBuffer = SpringBoneBufferBuilder.Build(avatarBase.transform, avatarShapeComponent.InstantiatedWearables, originalToClone);

            springBoneService.BufferCombiner.Register(newBuffer, oldBuffer);
            oldBuffer?.Dispose();

            registration.Buffer = newBuffer;
            registration.Clones = clones;
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CleanupOnDelete(in Entity entity, ref SpringBoneRegistrationComponent registration, ref SpringBonePendingCloneRelease pendingRelease)
        {
            UnregisterBuffer(ref registration);
            AddClonesToPendingRelease(registration.Clones, ref pendingRelease);
            registration.Clones = null;
            World.Remove<SpringBoneRegistrationComponent>(entity);
        }

        [Query]
        private void CleanupOnDispose(ref SpringBoneRegistrationComponent registration)
        {
            UnregisterBuffer(ref registration);

            // World is tearing down — release clones directly, no next frame
            if (registration.Clones != null)
            {
                foreach (Transform clone in registration.Clones)
                    if (clone != null) transformPool.Release(clone);

                registration.Clones = null;
            }
        }

        private void UnregisterBuffer(ref SpringBoneRegistrationComponent registration)
        {
            if (registration.Buffer != null)
            {
                springBoneService.BufferCombiner.Register(null, registration.Buffer);
                registration.Buffer.Dispose();
                registration.Buffer = null;
            }
        }

        private static void AddClonesToPendingRelease(Transform[] clones, ref SpringBonePendingCloneRelease pendingRelease)
        {
            if (clones == null) return;

            foreach (Transform clone in clones)
                if (clone != null) pendingRelease.Clones.Add(clone);
        }

        private static void PatchBoneArray(ref AvatarTransformMatrixComponent transformMatrixComponent, Dictionary<Transform, Transform> originalToClone)
        {
            if (originalToClone.Count == 0) return;

            Transform[] bones = transformMatrixComponent.bones.Inner;

            for (var i = 0; i < bones.Length; i++)
                if (bones[i] != null && originalToClone.TryGetValue(bones[i], out Transform clone)) bones[i] = clone;

            // Force pipeline re-registration so the TAA is rebuilt with clone transforms.
            // StartAvatarMatricesCalculationSystem checks IsValid() and re-registers when false.
            transformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.Uninitialized();
        }
    }
}
