using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.SpringBones
{
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(SpringBoneRegistrationSystem))]
    [UpdateBefore(typeof(StartAvatarMatricesCalculationSystem))]
    public partial class SpringBonesSimulationSystem : BaseUnityLoopSystem
    {
        private readonly SpringBoneService springBoneService;

        public SpringBonesSimulationSystem(World world, SpringBoneService springBoneService) : base(world)
        {
            this.springBoneService = springBoneService;
        }

        protected override void Update(float t)
        {
            SyncParentBonesQuery(World);

            springBoneService.Simulate(t);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SyncParentBones(in SpringBoneRegistrationComponent registration)
        {
            for (int i = 0; i < registration.SyncedBones.Count; i++)
            {
                (Transform wearableParent, Transform avatarParent) = registration.SyncedBones[i];
                wearableParent.SetPositionAndRotation(avatarParent.position, avatarParent.rotation);

                int slot = registration.Slots[i];
                springBoneService.UpdateParent(slot, avatarParent.rotation, avatarParent.localToWorldMatrix);
            }
        }
    }
}
