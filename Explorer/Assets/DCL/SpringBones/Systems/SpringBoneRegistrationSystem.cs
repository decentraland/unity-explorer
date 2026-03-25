using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
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
        private void RegisterNew(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase)
        {
            var wearables = avatarShapeComponent.InstantiatedWearables;
            FastSpringBoneBuffer buffer = SpringBoneBufferBuilder.Build(avatarBase.transform, wearables);

            if (buffer != null)
                springBoneService.BufferCombiner.Register(buffer, null);

            World.Add(entity, new SpringBoneRegistrationComponent
            {
                Buffer = buffer,
                LastKnownVersion = avatarShapeComponent.InstantiationVersion,
            });
        }

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(AvatarBase))]
        [None(typeof(DeleteEntityIntention))]
        private void ReRegisterOnChange(ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase,
            ref SpringBoneRegistrationComponent registration)
        {
            if (avatarShapeComponent.InstantiationVersion == registration.LastKnownVersion)
                return;

            registration.LastKnownVersion = avatarShapeComponent.InstantiationVersion;

            FastSpringBoneBuffer oldBuffer = registration.Buffer;
            FastSpringBoneBuffer newBuffer = SpringBoneBufferBuilder.Build(avatarBase.transform, avatarShapeComponent.InstantiatedWearables);

            springBoneService.BufferCombiner.Register(newBuffer, oldBuffer);
            oldBuffer?.Dispose();

            registration.Buffer = newBuffer;
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
    }
}
