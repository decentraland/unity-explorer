using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;

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
        private void SyncParentBones(ref SpringBoneRegistrationComponent registration)
        {
            for (int i = 0; i < registration.SyncPairs.Count; i++)
            {
                var (wearableParent, avatarParent) = registration.SyncPairs[i];
                wearableParent.SetPositionAndRotation(avatarParent.position, avatarParent.rotation);

                // Feed parent world transform to the simulation job
                if (i < registration.SlotIndices.Count)
                {
                    springBoneService.SetParentData(registration.SlotIndices[i],
                        avatarParent.rotation, avatarParent.localToWorldMatrix);
                }
            }
        }
    }
}
