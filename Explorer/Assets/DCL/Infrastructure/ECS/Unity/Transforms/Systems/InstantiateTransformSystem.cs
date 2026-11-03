using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace ECS.Unity.Transforms.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [ThrottlingEnabled]
    public partial class InstantiateTransformSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<Transform>? transformPool;

        public InstantiateTransformSystem(World world, IComponentPoolsRegistry componentPools) : base(world)
        {
            if (!componentPools.TryGetReferenceTypePool(out transformPool))
                ReportHub.LogError(GetReportData(), $"No component pool registered for {nameof(Transform)}");
        }

        protected override void Update(float _)
        {
            if (transformPool == null) return;

            InstantiateTransformQuery(World);
        }

        [Query]
        [All(typeof(SDKTransform))]
        [None(typeof(TransformComponent))]
        private void InstantiateTransform(in Entity entity, CRDTEntity sdkEntity)
        {
            World.Add(entity, transformPool!.CreateTransformComponent(entity, sdkEntity));
        }
    }
}
