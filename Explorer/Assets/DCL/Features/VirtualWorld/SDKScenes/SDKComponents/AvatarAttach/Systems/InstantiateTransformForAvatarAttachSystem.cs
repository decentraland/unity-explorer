using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.AvatarAttach.Systems
{
    /// <summary>
    ///     Provides special behavior if the AvatarAttach is not accompanied by SDKTransform from SDK side <br />
    ///     In this case Transform is manually added
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    [UpdateBefore(typeof(ParentingTransformSystem))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    [ThrottlingEnabled]
    public partial class InstantiateTransformForAvatarAttachSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<Transform> transformPool;
        private readonly Entity sceneRoot;

        private InstantiateTransformForAvatarAttachSystem(World world, IComponentPool<Transform> transformPool, Entity sceneRoot) : base(world)
        {
            this.transformPool = transformPool;
            this.sceneRoot = sceneRoot;
        }

        protected override void Update(float t)
        {
            InstantiateTransformQuery(World);
        }

        [Query]
        [All(typeof(PBAvatarAttach))]
        [None(typeof(TransformComponent), typeof(SDKTransform))] // if the transform was not instantiated by InstantiateTransformSystem
        private void InstantiateTransform(in Entity entity, CRDTEntity sdkEntity)
        {
            var transformComponent = transformPool.CreateTransformComponent(entity, sdkEntity);

            // Parenting system won't work as it relies on ParentId from SDKTransform
            // Assign sceneRoot as parent manually, we don't need any validation for this

            transformComponent.AssignParent(World.Reference(entity), World.Reference(sceneRoot), in World.Get<TransformComponent>(sceneRoot));

            transformComponent.SetTransform(Vector3.zero, Quaternion.identity, Vector3.one);

            World.Add(entity, transformComponent);
        }
    }
}
