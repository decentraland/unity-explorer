using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SpringBones
{
    /// <summary>
    ///     Handles spring bone registration and de-registration with the <see cref="springBoneService"/>.
    ///     Spring bones from GLTF assets need to be registered with the service for the simulation to take place.
    /// </summary>
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class SpringBoneRegistrationSystem : BaseUnityLoopSystem
    {
        // The service allows to register springs that need to be simulated.
        private readonly SpringBoneService springBoneService;

        public SpringBoneRegistrationSystem(World world, SpringBoneService springBoneService) : base(world)
        {
            this.springBoneService = springBoneService;
        }

        protected override void Update(float t)
        {
            CreateRegistrationQuery(World);
            BuildSpringsQuery(World);
            CleanUpOnDeleteQuery(World);
        }

        protected override void OnDispose() =>
            CleanUpOnDisposeQuery(World);

        /// <summary>
        ///     Creates the registration component that holds data about which springs are being simulated for a given avatar.
        /// </summary>
        [Query]
        [None(typeof(SpringBoneRegistrationComponent), typeof(DeleteEntityIntention))]
        private void CreateRegistration(in Entity entity) =>
            World.Add(entity, new SpringBoneRegistrationComponent
            {
                AvatarVersion = -1,
                Slots = ListPool<int>.Get(),
                SyncedBones = ListPool<(Transform, Transform)>.Get(),
            });

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void BuildSprings(ref SpringBoneRegistrationComponent registration,
            ref AvatarShapeComponent avatarShapeComponent,
            ref AvatarTransformMatrixComponent transformMatrixComponent,
            AvatarBase avatarBase)
        {
            // Gate the update by avatar version (could use a dirty marker component instead)
            if (registration.AvatarVersion == avatarShapeComponent.InstantiationVersion) return;

            registration.AvatarVersion = avatarShapeComponent.InstantiationVersion;

            // Reset state
            FreeSlots(registration);
            registration.SyncedBones.Clear();

            using var springJointsScope = ListPool<Transform>.Get(out var springJoints);

            // Creates the data to register with the service
            // Returns the registered bones through the list argument
            SpringBonesRegistrationHelper.RegisterSprings(springBoneService,
                avatarBase.AvatarSkinnedMeshRenderer,
                avatarShapeComponent.InstantiatedWearables,
                registration,
                springJoints);

            // Append the additional bones to the custom skinning buffers
            if (springJoints.Count > 0)
            {
                transformMatrixComponent.bones.Append(springJoints);
                transformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.Uninitialized();
            }
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CleanUpOnDelete(Entity entity, ref SpringBoneRegistrationComponent registration) =>
            CleanUp(entity, ref registration);

        [Query]
        private void CleanUpOnDispose(Entity entity, ref SpringBoneRegistrationComponent registration) =>
            CleanUp(entity, ref registration);

        private void CleanUp(Entity entity, ref SpringBoneRegistrationComponent registration)
        {
            FreeSlots(registration);

            ListPool<int>.Release(registration.Slots);
            ListPool<(Transform, Transform)>.Release(registration.SyncedBones);

            World.Remove<SpringBoneRegistrationComponent>(entity);
        }

        private void FreeSlots(in SpringBoneRegistrationComponent registration)
        {
            foreach (int slot in registration.Slots) springBoneService.UnregisterSpring(slot);
            registration.Slots.Clear();
        }
    }
}
