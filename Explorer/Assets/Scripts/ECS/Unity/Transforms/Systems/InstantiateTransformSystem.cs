using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using Utility;

namespace ECS.Unity.Transforms.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [ThrottlingEnabled]
    public partial class InstantiateTransformSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<Transform> transformPool;

        public InstantiateTransformSystem(World world, IComponentPoolsRegistry componentPools) : base(world)
        {
            transformPool = componentPools.GetReferenceTypePool<Transform>();
        }

        protected override void Update(float _)
        {
            InstantiateTransformQuery(World);
        }

        [Query]
        [All(typeof(SDKTransform))]
        [None(typeof(TransformComponent))]
        private void InstantiateTransform(in Entity entity)
        {
            Transform newTransform = transformPool.Get();
            newTransform.name = "Entity " + entity.Id;
            var transformComponent = new TransformComponent(newTransform);
            World.Add(entity, transformComponent);
        }
    }
}
