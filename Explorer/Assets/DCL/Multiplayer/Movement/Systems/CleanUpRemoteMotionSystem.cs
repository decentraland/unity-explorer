using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class CleanUpRemoteMotionSystem : BaseUnityLoopSystem
    {
        internal CleanUpRemoteMotionSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            DisposeMovementComponentQuery(World);
        }

        [Query]
        private void DisposeMovementComponent(ref RemotePlayerMovementComponent movementComponent, in DeleteEntityIntention deleteEntityIntention)
        {
            if (!deleteEntityIntention.DeferDeletion)
                movementComponent.Dispose();
        }
    }
}
