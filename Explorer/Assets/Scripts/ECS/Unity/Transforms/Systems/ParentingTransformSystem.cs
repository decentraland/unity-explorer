using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;

namespace ECS.Unity.Transforms.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    [ThrottlingEnabled]
    public partial class ParentingTransformSystem : BaseUnityLoopSystem
    {
        private readonly Entity sceneRoot;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;

        public ParentingTransformSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, Entity sceneRoot) : base(world)
        {
            this.sceneRoot = sceneRoot;
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            OrphanChildrenOfDeletedEntityQuery(World);
            DoParentingQuery(World);
        }

        [Query]
        [All(typeof(SDKTransform), typeof(TransformComponent), typeof(DeleteEntityIntention))]
        private void OrphanChildrenOfDeletedEntity(ref TransformComponent transformComponentToBeDeleted)
        {
            foreach (EntityReference childEntity in transformComponentToBeDeleted.Children)
            {
                SetNewChild(ref World.Get<TransformComponent>(childEntity.Entity),
                    childEntity, sceneRoot);
            }

            transformComponentToBeDeleted.Children.Clear();
        }

        [Query]
        [All(typeof(SDKTransform), typeof(TransformComponent))]
        private void DoParenting(in Entity entity, ref SDKTransform sdkTransform, ref TransformComponent transformComponent)
        {
            if (!sdkTransform.IsDirty) return;

            Entity parentReference = sceneRoot;

            if (entitiesMap.TryGetValue(sdkTransform.ParentId, out Entity newParentEntity))
            {
                parentReference = newParentEntity;

                //We have to remove the child from the old parent
                if (transformComponent.Parent != parentReference)
                    RemoveFromParent(transformComponent, World.Reference(entity));
            }

            SetNewChild(ref transformComponent, World.Reference(entity), parentReference);
        }

        private void SetNewChild(ref TransformComponent childComponent, EntityReference childEntityReference,
            Entity parentEntity)
        {
            if (childComponent.Parent == parentEntity)
                return;

            if (!World.IsAlive(parentEntity))
            {
                ReportHub.LogError(GetReportCategory(), $"Trying to parent entity {childEntityReference.Entity} to a dead entity parent");
                return;
            }

            ref TransformComponent parentComponent = ref World.TryGetRef<TransformComponent>(parentEntity, out bool success);

            if (!success)
            {
                ReportHub.LogError(GetReportCategory(), $"Trying to parent entity {childEntityReference.Entity} to parent {parentEntity} that doesn't have a TransformComponent");
                return;
            }

            childComponent.Transform.SetParent(parentComponent.Transform, true);
            childComponent.Parent = World.Reference(parentEntity);
            parentComponent.Children.Add(childEntityReference);
        }

        private void RemoveFromParent(TransformComponent childComponent, EntityReference childEntityReference)
        {
            if (childComponent.Parent.IsAlive(World))
                World.Get<TransformComponent>(childComponent.Parent.Entity).Children.Remove(childEntityReference);
        }
    }
}
