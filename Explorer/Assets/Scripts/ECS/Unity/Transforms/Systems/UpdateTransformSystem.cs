using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.UnityBridge;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.UpdateGate;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using System;
using CrdtEcsBridge.Components.Special;

namespace ECS.Unity.Transforms.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParentingTransformSystem))]
    public partial class UpdateTransformSystem : BaseUnityLoopSystem
    {
        internal static readonly Type THROTTLING_GROUP_TYPE = typeof(SimulationSystemGroup);

        private readonly ISystemGroupsUpdateGate ecsGroupThrottler;
        private readonly ISystemsUpdateGate systemsPriorityComponentsGate;

        public UpdateTransformSystem(World world, ISystemGroupsUpdateGate ecsGroupThrottler, ISystemsUpdateGate systemsPriorityComponentsGate) : base(world)
        {
            this.ecsGroupThrottler = ecsGroupThrottler;
            this.systemsPriorityComponentsGate = systemsPriorityComponentsGate;
        }

        protected override void Update(float _)
        {
            if (systemsPriorityComponentsGate.IsOpen<SDKTransform>())
                UpdateTransformQuery(World);
            else if (!ecsGroupThrottler.ShouldThrottle(THROTTLING_GROUP_TYPE, new TimeProvider.Info()))
                UpdateTransformQuery(World);
        }

        [Query]
        [None(typeof(SceneRootComponent))]
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
