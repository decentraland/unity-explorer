using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UniGLTF.SpringBoneJobs.InputPorts;
using UniVRM10.FastSpringBones;

namespace DCL.SpringBones
{
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class SpringBoneRegistrationSystem : BaseUnityLoopSystem
    {
        private readonly FastSpringBoneService springBoneService;

        internal SpringBoneRegistrationSystem(World world, FastSpringBoneService springBoneService) : base(world)
        {
            this.springBoneService = springBoneService;
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
        [All(typeof(AvatarShapeComponent), typeof(AvatarCustomSkinningComponent), typeof(AvatarBase))]
        [None(typeof(SpringBoneRegistrationComponent), typeof(DeleteEntityIntention))]
        private void RegisterNew(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase,
            ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            IList<CachedAttachment> wearables = avatarShapeComponent.InstantiatedWearables;
            int fingerprint = ComputeWearableFingerprint(wearables, transformMatrixComponent.bones);
            FastSpringBoneBuffer buffer = SpringBoneBufferBuilder.Build(avatarBase.transform, wearables);

            if (buffer != null)
                springBoneService.BufferCombiner.Register(buffer, null);

            World.Add(entity, new SpringBoneRegistrationComponent
            {
                Buffer = buffer,
                WearableFingerprint = fingerprint
            });
        }

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(AvatarBase))]
        [None(typeof(DeleteEntityIntention))]
        private void ReRegisterOnChange(ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase,
            ref AvatarTransformMatrixComponent transformMatrixComponent,
            ref SpringBoneRegistrationComponent registration)
        {
            if (avatarShapeComponent.IsDirty)
                return;

            IList<CachedAttachment> wearables = avatarShapeComponent.InstantiatedWearables;
            int currentFingerprint = ComputeWearableFingerprint(wearables, transformMatrixComponent.bones);

            if (currentFingerprint == registration.WearableFingerprint)
                return;

            FastSpringBoneBuffer oldBuffer = registration.Buffer;
            FastSpringBoneBuffer newBuffer = SpringBoneBufferBuilder.Build(avatarBase.transform, wearables);

            springBoneService.BufferCombiner.Register(newBuffer, oldBuffer);
            oldBuffer?.Dispose();

            registration.Buffer = newBuffer;
            registration.WearableFingerprint = currentFingerprint;
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CleanupOnDelete(in Entity entity, ref SpringBoneRegistrationComponent registration)
        {
            UnregisterAndDispose(ref registration);
            World.Remove<SpringBoneRegistrationComponent>(entity);
        }

        [Query]
        private void CleanupOnDispose(ref SpringBoneRegistrationComponent registration)
        {
            UnregisterAndDispose(ref registration);
        }

        private void UnregisterAndDispose(ref SpringBoneRegistrationComponent registration)
        {
            if (registration.Buffer != null)
            {
                springBoneService.BufferCombiner.Register(null, registration.Buffer);
                registration.Buffer.Dispose();
                registration.Buffer = null;
            }
        }

        internal static int ComputeWearableFingerprint(IList<CachedAttachment> wearables, BoneArray bones)
        {
            int count = wearables.Count;

            if (count == 0)
                return 0;

            int hash = count;

            for (int i = 0; i < count; i++)
                hash = hash * 397 ^ RuntimeHelpers.GetHashCode(wearables[i].Instance);

            // BoneArray.Inner is a new Transform[] on every re-instantiation,
            // so its identity hash changes even when the same cached wearables are reused
            hash = hash * 397 ^ RuntimeHelpers.GetHashCode(bones.Inner);

            return hash;
        }

        private static int ComputeWearableOnlyFingerprint(IList<CachedAttachment> wearables)
        {
            int count = wearables.Count;

            if (count == 0)
                return 0;

            int hash = count;

            for (int i = 0; i < count; i++)
                hash = hash * 397 ^ RuntimeHelpers.GetHashCode(wearables[i].Instance);

            return hash;
        }
    }
}
