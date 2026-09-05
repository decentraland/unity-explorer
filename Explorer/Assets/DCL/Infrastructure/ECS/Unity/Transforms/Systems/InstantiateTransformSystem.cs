using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.Optimization.Pools;
using ECS.Abstract;
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
        private void InstantiateTransform(in Entity entity, CRDTEntity sdkEntity)
        {
            World.Add(entity, transformPool.CreateTransformComponent(entity, sdkEntity));
        }
    }
}
