using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace ECS.Unity.Transforms.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParentingTransformSystem))]
    public partial class UpdateTransformSystem : BaseUnityLoopSystem
    {
        public UpdateTransformSystem(World world) : base(world) { }

        protected override void Update(float _)
        {
            UpdateTransformQuery(World);
        }

        [Query]
        [All(typeof(SDKTransform), typeof(TransformComponent))]
        private void UpdateTransform(ref SDKTransform sdkTransform, ref TransformComponent transformComponent)
        {
            if (sdkTransform.IsDirty)
            {
                Transform unityTransform = transformComponent.Transform;
                unityTransform.localPosition = sdkTransform.Position;
                unityTransform.localRotation = sdkTransform.Rotation;
                unityTransform.localScale = sdkTransform.Scale;
                sdkTransform.IsDirty = false;
            }
        }
    }
}
