using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.Diagnostics;
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

        public SpringBoneRegistrationSystem(World world, FastSpringBoneService springBoneService) : base(world)
        {
            this.springBoneService = springBoneService;
        }

        protected override void Update(float t)
        {
            RegisterNewQuery(World);
            ReRegisterOnChangeQuery(World);
            CleanUpOnDeleteQuery(World);
        }

        protected override void OnDispose() =>
            CleanUpOnDisposeQuery(World);

        [Query]
        [None(typeof(SpringBoneRegistrationComponent), typeof(DeleteEntityIntention))]
        private void RegisterNew(in Entity entity,
            ref AvatarShapeComponent avatarShapeComponent,
            AvatarBase avatarBase,
            ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            var syncPairs = ListPool<(Transform, Transform)>.Get();

            FastSpringBoneBuffer buffer = BuildSpringBoneBuffer(avatarShapeComponent.InstantiatedWearables,
                avatarBase,
                ref transformMatrixComponent,
                syncPairs,
                null);

            World.Add(entity,
                new SpringBoneRegistrationComponent
                {
                    Buffer = buffer,
                    AvatarVersion = avatarShapeComponent.InstantiationVersion,
                    SyncPairs = syncPairs,
                });
        }

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(AvatarBase), typeof(AvatarTransformMatrixComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void ReRegisterOnChange(ref AvatarShapeComponent avatarShapeComponent,
            AvatarBase avatarBase,
            ref SpringBoneRegistrationComponent registration,
            ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            if (avatarShapeComponent.InstantiationVersion == registration.AvatarVersion) return;

            registration.AvatarVersion = avatarShapeComponent.InstantiationVersion;
            registration.SyncPairs.Clear();

            registration.Buffer = BuildSpringBoneBuffer(avatarShapeComponent.InstantiatedWearables,
                avatarBase,
                ref transformMatrixComponent,
                registration.SyncPairs,
                registration.Buffer);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CleanUpOnDelete(ref SpringBoneRegistrationComponent registration) =>
            CleanUpSpringBoneBuffer(ref registration);

        [Query]
        private void CleanUpOnDispose(ref SpringBoneRegistrationComponent registration) =>
            CleanUpSpringBoneBuffer(ref registration);

        private FastSpringBoneBuffer BuildSpringBoneBuffer(IList<CachedAttachment> wearables,
            AvatarBase avatarBase,
            ref AvatarTransformMatrixComponent transformMatrixComponent,
            List<(Transform wearableParent, Transform skeletonBone)> syncPairs,
            FastSpringBoneBuffer? oldBuffer)
        {
            Transform[] skeleton = avatarBase.AvatarSkinnedMeshRenderer.bones;

            using var springBoneTransformsScope = ListPool<Transform>.Get(out var springBoneTransforms);

            foreach (CachedAttachment wearable in wearables)
            foreach (SpringBoneData springBone in wearable.SpringBones)
            {
                springBoneTransforms.Add(springBone.ManagedTransform);

                // Reset local rotation (game objects are reused due to pooling, we want bones to be in their initial state)
                springBone.ManagedTransform.localRotation = springBone.InitialLocalRotation;

                if (!springBone.IsRoot) continue;

                // Since spring bones live in the wearable game objects we sync their parents to the corresponding avatar bone
                Transform wearableParent = springBone.ManagedTransform.parent;
                Transform avatarParent = skeleton[springBone.AvatarSkeletonParentBoneIndex];
                wearableParent.SetPositionAndRotation(avatarParent.position, avatarParent.rotation);

                // Store for later, we need to sync every frame
                syncPairs.Add((wearableParent, avatarParent));
            }

            FastSpringBoneBuffer newBuffer = SpringBoneBufferBuilder.Build(avatarBase.transform, wearables);

            springBoneService.BufferCombiner.Register(newBuffer, oldBuffer);

            if (springBoneTransforms.Count > 0)
            {
                transformMatrixComponent.bones.Append(springBoneTransforms);
                transformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.Uninitialized();
            }

            return newBuffer;
        }

        private void CleanUpSpringBoneBuffer(ref SpringBoneRegistrationComponent registration)
        {
            if (registration.SyncPairs != null)
            {
                ListPool<(Transform, Transform)>.Release(registration.SyncPairs);
                registration.SyncPairs = null;
            }

            if (registration.Buffer != null)
            {
                springBoneService.BufferCombiner.Register(null, registration.Buffer);

                registration.Buffer.Dispose();
                registration.Buffer = null;
            }
        }
    }
}
