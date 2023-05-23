using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace ECS.Unity.Transforms.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ParentingTransformSystem))]
    public class UpdateTransformSystem : BaseUnityLoopSystem
    {
        public UpdateTransformSystem(World world) : base(world) { }

        protected override void Update(float _)
        {
            UpdateTransformQuery(World);
        }

        [Query]
        [All(typeof(SDKTransform), typeof(TransformComponent))]
        private void UpdateTransform(in Entity entity, ref SDKTransform sdkTransform, ref TransformComponent transformComponent)
        {
            if (sdkTransform.IsDirty)
            {
                Transform unityTransform = transformComponent.Transform;
                unityTransform.position = sdkTransform.Position;
                unityTransform.rotation = sdkTransform.Rotation;
                unityTransform.localScale = sdkTransform.Scale;
                sdkTransform.IsDirty = false;
            }
        }
    }
}
