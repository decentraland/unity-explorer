using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UniVRM10.FastSpringBones;

namespace DCL.SpringBones
{
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(SpringBoneRegistrationSystem))]
    [UpdateBefore(typeof(StartAvatarMatricesCalculationSystem))]
    public partial class SpringBonesSimulationSystem : BaseUnityLoopSystem
    {
        private readonly FastSpringBoneService springBoneService;

        public SpringBonesSimulationSystem(World world, FastSpringBoneService springBoneService) : base(world)
        {
            this.springBoneService = springBoneService;
        }

        protected override void Update(float t)
        {
            SyncParentBonesQuery(World);
            springBoneService.ManualUpdate(t);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SyncParentBones(ref SpringBoneRegistrationComponent registration)
        {
            foreach (var (wearableParent, avatarParent) in registration.SyncPairs)
                wearableParent.SetPositionAndRotation(avatarParent.position, avatarParent.rotation);
        }
    }
}
