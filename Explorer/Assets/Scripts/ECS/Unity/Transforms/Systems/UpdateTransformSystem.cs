using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;

namespace ECS.Unity.Transforms.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParentingTransformSystem))]
    [ThrottlingEnabled]
    public partial class UpdateTransformSystem : BaseUnityLoopSystem
    {
        public UpdateTransformSystem(World world) : base(world) { }

        protected override void Update(float _)
        {
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
