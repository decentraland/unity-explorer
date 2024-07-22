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

        public UpdateTransformSystem(World world, ISystemGroupsUpdateGate ecsGroupThrottler) : base(world)
        {
            this.ecsGroupThrottler = ecsGroupThrottler;
        }

        protected override void Update(float _)
        {
            if (ecsGroupThrottler.ShouldThrottle(typeof(SyncedSimulationSystemGroup), new TimeProvider.Info()))
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
