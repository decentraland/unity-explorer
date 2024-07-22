using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.UnityBridge;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.UpdateGate;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;

namespace ECS.Unity.Transforms.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParentingTransformSystem))]
    public partial class UpdateTransformSystem : BaseUnityLoopSystem
    {
        private readonly ISystemGroupsUpdateGate ecsGroupThrottler;
        private readonly ISystemsUpdateGate systemsUpdateDirtyMarkerPriorityGate;

        public UpdateTransformSystem(World world, ISystemGroupsUpdateGate ecsGroupThrottler, ISystemsUpdateGate systemsUpdateDirtyMarkerPriorityGate) : base(world)
        {
            this.ecsGroupThrottler = ecsGroupThrottler;
            this.systemsUpdateDirtyMarkerPriorityGate = systemsUpdateDirtyMarkerPriorityGate;
        }

        protected override void Update(float _)
        {
            if (systemsUpdateDirtyMarkerPriorityGate.IsClosed<SDKTransform>() && ecsGroupThrottler.ShouldThrottle(typeof(SyncedSimulationSystemGroup), new TimeProvider.Info()))
                return;

            UpdateTransformQuery(World);
        }

        [Query]
        private void UpdateTransform(ref SDKTransform sdkTransform, ref TransformComponent transformComponent)
        {
            if (sdkTransform.IsDirty)
            {
                transformComponent.SetTransform(sdkTransform.Position, sdkTransform.Rotation, sdkTransform.Scale);
                sdkTransform.IsDirty = false;
            }
        }
    }
}
