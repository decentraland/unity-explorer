using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.UnityBridge;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.UpdateGate;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using System;

namespace ECS.Unity.Transforms.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParentingTransformSystem))]
    public partial class UpdateTransformSystem : BaseUnityLoopSystem
    {
        private readonly ISystemGroupsUpdateGate ecsGroupThrottler;
        private readonly ISystemsUpdateGate systemsPriorityComponentsGate;

        private static readonly Type PARENT_GROUP_TYPE = ((UpdateInGroupAttribute)Attribute
           .GetCustomAttribute(typeof(UpdateTransformSystem), typeof(UpdateInGroupAttribute)))?.GroupType!;

        public UpdateTransformSystem(World world, ISystemGroupsUpdateGate ecsGroupThrottler, ISystemsUpdateGate systemsPriorityComponentsGate) : base(world)
        {
            this.ecsGroupThrottler = ecsGroupThrottler;
            this.systemsPriorityComponentsGate = systemsPriorityComponentsGate;
        }

        protected override void Update(float _)
        {
            if (systemsPriorityComponentsGate.IsOpen<SDKTransform>())
                UpdateTransformQuery(World);
            else if (!ecsGroupThrottler.ShouldThrottle(PARENT_GROUP_TYPE, new TimeProvider.Info()))
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
